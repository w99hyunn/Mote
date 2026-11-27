using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

const string issuer = "https://player-auth.services.api.unity.com";
var builder = WebApplication.CreateBuilder(args);
var projectId = Guid.Parse(builder.Configuration["Unity:ProjectId"]
    ?? throw new InvalidOperationException("Unity:ProjectId is required."));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.IncludeErrorDetails = false;
        options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            issuer + "/.well-known/jwks.json", new UnitySigningKeys(), new HttpDocumentRetriever());
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = issuer,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidAudience = $"upid:{projectId:D}",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var player = context.Principal!;
                if (!Guid.TryParse(player.FindFirstValue("project_id"), out var tokenProject)
                    || tokenProject != projectId || string.IsNullOrWhiteSpace(player.FindFirstValue("sub")))
                    context.Fail("Invalid player or Unity project.");
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddDataProtection();
builder.Services.AddHttpClient("Google");
builder.Services.AddSingleton(_ => Npgsql.NpgsqlDataSource.Create(
    builder.Configuration.GetConnectionString("Planets")
    ?? throw new InvalidOperationException("ConnectionStrings:Planets is required.")));
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGoogleDesktopLogin();
await app.MapPlanetsAsync();
app.MapPost("/api/auth/login", (ClaimsPrincipal player) =>
    Results.Ok(new { playerId = player.FindFirstValue("sub") })).RequireAuthorization();
app.Run();
