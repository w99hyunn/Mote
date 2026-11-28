using System.Security.Claims;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

internal static class Planets
{
    internal record Item(string Kind, float X, float Y, float Z, string? GiftFrom = null, int Yaw = 0);
    internal record Gift(int Revision, int Slot);
    internal record Layout(int Revision, Item[] Items, int? Sky = null);
    internal record Note(string Id, string Text);

    internal static async Task MapPlanetsAsync(this WebApplication app)
    {
        var data = app.Services.GetRequiredService<NpgsqlDataSource>();
        await using (var db = await data.OpenConnectionAsync())
        await using (var transaction = await db.BeginTransactionAsync())
        {
            await using var schema = new NpgsqlCommand("""
                SELECT pg_advisory_xact_lock(935741, 1);
                CREATE SCHEMA IF NOT EXISTS mote;
                CREATE TABLE IF NOT EXISTS mote.planets (
                    owner text PRIMARY KEY,
                    revision integer NOT NULL DEFAULT 0,
                    items jsonb NOT NULL DEFAULT '[]'::jsonb
                );
                ALTER TABLE mote.planets ADD COLUMN IF NOT EXISTS sky integer NOT NULL DEFAULT 0;
                CREATE TABLE IF NOT EXISTS mote.notes (
                    seq bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    owner text NOT NULL REFERENCES mote.planets(owner) ON DELETE CASCADE,
                    author text NOT NULL,
                    id uuid NOT NULL,
                    body text NOT NULL,
                    UNIQUE (owner, author, id)
                );
                CREATE INDEX IF NOT EXISTS notes_owner_seq ON mote.notes(owner, seq DESC);
                """, db, transaction);
            await schema.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        var routes = app.MapGroup("/api/planets").RequireAuthorization();
        routes.MapGet("/{id}", Read);
        routes.MapPut("/me/layout", Save);
        routes.MapPost("/{id}/notes", Write);
        routes.MapPost("/{id}/gifts", LeaveGift);
    }

    private static async Task<IResult> Read(string id, ClaimsPrincipal user, NpgsqlDataSource data, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        if (id == user.FindFirstValue("sub"))
        {
            await using var ensure = new NpgsqlCommand("INSERT INTO mote.planets(owner) VALUES ($1) ON CONFLICT DO NOTHING", db);
            ensure.Parameters.Add(new NpgsqlParameter<string> { TypedValue = id });
            await ensure.ExecuteNonQueryAsync(ct);
        }
        await using var cmd = new NpgsqlCommand("""
            SELECT revision, items, (
                SELECT COALESCE(jsonb_agg(to_jsonb(n) - 'seq' ORDER BY n.seq DESC), '[]'::jsonb)
                FROM (SELECT seq, author, body AS text FROM mote.notes WHERE owner=$1 ORDER BY seq DESC LIMIT 30) n
            ) AS notes, sky FROM mote.planets WHERE owner=$1
            """, db);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = id });
        await using var row = await cmd.ExecuteReaderAsync(ct);
        if (!await row.ReadAsync(ct)) return Results.NotFound();
        return Results.Ok(new { owner = id, revision = row.GetInt32(0), sky = row.GetInt32(3),
            items = JsonSerializer.Deserialize<Item[]>(row.GetString(1)),
            notes = JsonSerializer.Deserialize<JsonElement>(row.GetString(2)) });
    }

    private static async Task<IResult> Save(Layout layout, ClaimsPrincipal user, NpgsqlDataSource data, CancellationToken ct)
    {
        if (layout.Sky is < 0 or > 3 || layout.Items == null || layout.Items.Length > 48 || layout.Items.Any(i =>
            i == null || i.Kind is not ("tree" or "flower" or "stone" or "bench" or "tent" or "lamp" or "pond" or "cloud") ||
            i.Yaw is < 0 or >= 360 || i.Yaw % 45 != 0 ||
            Math.Abs(i.X*i.X+i.Y*i.Y+i.Z*i.Z-1) > .015f)) return Results.BadRequest();
        // A gift belongs to the original host-approved site. Only the gift endpoint can create one.
        await using var cmd = data.CreateCommand("""
            UPDATE mote.planets p SET revision=revision+1, sky=COALESCE($4, p.sky), items=(
                SELECT coalesce(jsonb_agg((incoming.item - 'GiftFrom') || coalesce((
                    SELECT jsonb_build_object('GiftFrom', old->'GiftFrom') FROM jsonb_array_elements(p.items) old
                    WHERE old->>'GiftFrom' IS NOT NULL AND old - 'GiftFrom' - 'Yaw'=incoming.item - 'GiftFrom' - 'Yaw' LIMIT 1
                ), '{}'::jsonb) ORDER BY incoming.pos), '[]'::jsonb)
                FROM jsonb_array_elements($1) WITH ORDINALITY incoming(item,pos)
            ) WHERE owner=$2 AND revision=$3
            """);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = JsonSerializer.Serialize(layout.Items) });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = user.FindFirstValue("sub")! });
        cmd.Parameters.Add(new NpgsqlParameter<int> { TypedValue = layout.Revision });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Integer, Value = (object?)layout.Sky ?? DBNull.Value });
        return await cmd.ExecuteNonQueryAsync(ct) == 1 ? Results.NoContent() : Results.Conflict();
    }

    private static async Task<IResult> LeaveGift(string id, Gift gift, ClaimsPrincipal user, NpgsqlDataSource data, CancellationToken ct)
    {
        var visitor = user.FindFirstValue("sub")!;
        if (id == visitor || gift.Slot is < 0 or >= 48) return Results.BadRequest();
        await using var cmd = data.CreateCommand("""
            UPDATE mote.planets SET items=jsonb_set(items, ARRAY[($1::int)::text,'GiftFrom'], to_jsonb($2::text)), revision=revision+1
            WHERE owner=$3 AND revision=$4 AND items->$1->>'Kind' IN ('pond','cloud') AND items->$1->>'GiftFrom' IS NULL
            """);
        cmd.Parameters.Add(new NpgsqlParameter<int> { TypedValue = gift.Slot });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = visitor });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = id });
        cmd.Parameters.Add(new NpgsqlParameter<int> { TypedValue = gift.Revision });
        return await cmd.ExecuteNonQueryAsync(ct) == 1 ? Results.NoContent() : Results.Conflict();
    }

    private static async Task<IResult> Write(string id, Note note, ClaimsPrincipal user, NpgsqlDataSource data, CancellationToken ct)
    {
        if (!Guid.TryParse(note.Id, out var key) || string.IsNullOrWhiteSpace(note.Text) || note.Text.Trim().Length > 80)
            return Results.BadRequest();
        await using var cmd = data.CreateCommand("""
            INSERT INTO mote.notes(owner, author, id, body) VALUES ($1,$2,$3,$4)
            ON CONFLICT (owner, author, id) DO NOTHING
            """);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = id });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = user.FindFirstValue("sub")! });
        cmd.Parameters.Add(new NpgsqlParameter<Guid> { TypedValue = key });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = note.Text.Trim() });
        try { await cmd.ExecuteNonQueryAsync(ct); }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.ForeignKeyViolation) { return Results.NotFound(); }
        return Results.NoContent();
    }
}
