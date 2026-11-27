using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

internal static class GoogleDesktopLogin
{
    internal static void MapGoogleDesktopLogin(this WebApplication app)
    {
        var settings = app.Configuration.GetSection("Google");
        var protector = app.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("Mote.GoogleDesktopLogin.v1").ToTimeLimitedDataProtector();
        var routes = app.MapGroup("/api/auth/google");
        routes.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            context.HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
            return await next(context);
        });
        routes.MapMethods("/start", ["HEAD", "GET"], (HttpContext http, int port, string challenge, string state) =>
        {
            if (port is < 1024 or > 65535 || !IsRandomValue(challenge) || !IsRandomValue(state))
                return Results.BadRequest();
            if (string.IsNullOrWhiteSpace(settings["ClientSecret"]) || string.IsNullOrWhiteSpace(settings["ClientId"])
                || !Uri.TryCreate(settings["RedirectUri"], UriKind.Absolute, out var redirect) || redirect.Scheme != "https")
                return Results.Text("서버에 Google 로그인 설정이 필요합니다.", statusCode: 503);
            if (HttpMethods.IsHead(http.Request.Method)) return Results.NoContent();

            var lifetime = TimeSpan.FromMinutes(5);
            var ticket = protector.Protect($"{port}:{state}", lifetime);
            http.Response.Cookies.Append("__Host-MoteGoogle-" + state, "1", new CookieOptions
            { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Lax, Path = "/", MaxAge = lifetime });
            return Results.Redirect(QueryHelpers.AddQueryString("https://accounts.google.com/o/oauth2/v2/auth",
                new Dictionary<string, string?> {
                    ["client_id"] = settings["ClientId"], ["redirect_uri"] = redirect.AbsoluteUri,
                    ["response_type"] = "code", ["scope"] = "openid", ["state"] = ticket,
                    ["code_challenge"] = challenge, ["code_challenge_method"] = "S256", ["prompt"] = "select_account"
                }));
        });
        routes.MapGet("/callback", (HttpContext http, string state, string? code, string? error) =>
        {
            string[] parts;
            try { parts = protector.Unprotect(state).Split(':'); }
            catch (CryptographicException) { return Results.BadRequest(); }
            var cookie = "__Host-MoteGoogle-" + parts[1];
            if (!http.Request.Cookies.ContainsKey(cookie)) return Results.BadRequest();
            http.Response.Cookies.Delete(cookie, new CookieOptions { Secure = true, Path = "/" });
            var result = new Dictionary<string, string?> { ["state"] = parts[1] };
            if (error == null && !string.IsNullOrEmpty(code)) result["code"] = code;
            else result["error"] = error == "access_denied" ? "cancelled" : "failed";
            return Results.Redirect(QueryHelpers.AddQueryString($"http://127.0.0.1:{parts[0]}/mote/", result));
        });
        routes.MapPost("/exchange", async (Exchange input, IHttpClientFactory clients, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(input.Code) || input.Code.Length > 4096 || !IsRandomValue(input.Verifier))
                return Results.BadRequest();
            if (string.IsNullOrWhiteSpace(settings["ClientSecret"]) || string.IsNullOrWhiteSpace(settings["RedirectUri"]))
                return Results.StatusCode(503);
            using var form = new FormUrlEncodedContent(new Dictionary<string, string> {
                ["grant_type"] = "authorization_code", ["client_id"] = settings["ClientId"]!,
                ["client_secret"] = settings["ClientSecret"]!, ["redirect_uri"] = settings["RedirectUri"]!,
                ["code"] = input.Code, ["code_verifier"] = input.Verifier
            });
            using var reply = await clients.CreateClient("Google").PostAsync("https://oauth2.googleapis.com/token", form, ct);
            if (!reply.IsSuccessStatusCode) return Results.BadRequest();
            using var json = JsonDocument.Parse(await reply.Content.ReadAsStreamAsync(ct));
            return json.RootElement.TryGetProperty("id_token", out var token)
                ? Results.Ok(new { idToken = token.GetString() }) : Results.BadRequest();
        });
    }

    private static bool IsRandomValue(string? value) => value is { Length: 43 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    internal sealed record Exchange(string Code, string Verifier);
}
