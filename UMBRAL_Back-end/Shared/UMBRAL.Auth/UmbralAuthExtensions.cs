namespace UMBRAL.Auth;

using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Auth helpers shared by all UMBRAL .NET services (HU-23).
///
/// All five services point to the same Keycloak realm and trust tokens
/// issued by it. To keep <c>Program.cs</c> short and to make sure every
/// service validates tokens with identical rules, the JwtBearer setup
/// lives here and is consumed via <see cref="AddUmbralJwtAuth"/>.
///
/// Configuration expected (in <c>appsettings.json</c>):
/// <code>
/// {
///   "Keycloak": {
///     "Authority": "http://localhost:8090/realms/umbral",
///     "Audience":  "account"
///   }
/// }
/// </code>
/// </summary>
public static class UmbralAuthExtensions
{
    public const string SchemeName = JwtBearerDefaults.AuthenticationScheme;

    /// <summary>
    /// Registers JwtBearer pointing to Keycloak. Authentication is OPTIONAL —
    /// only endpoints decorated with <c>[Authorize]</c> require a valid token;
    /// the rest stay public (and can still read the operator's claims from
    /// <c>HttpContext.User</c> when a token is present).
    /// </summary>
    public static IServiceCollection AddUmbralJwtAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var authority = configuration["Keycloak:Authority"]
            ?? "http://localhost:8090/realms/umbral";
        var audience  = configuration["Keycloak:Audience"] ?? "account";

        // Prevent the default JWT handler from rewriting standard claim types
        // (sub, name, roles, …) so we can match them by their JWT names.
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        services
            .AddAuthentication(SchemeName)
            .AddJwtBearer(SchemeName, options =>
            {
                options.Authority = authority;
                options.Audience  = audience;
                // Dev environment runs Keycloak over plain HTTP. Set to true
                // before deploying to production.
                options.RequireHttpsMetadata = false;
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidIssuer              = authority,
                    // ValidateAudience disabled: Keycloak emits tokens with
                    // aud = clientId (e.g. "umbral-frontend") by default, not
                    // "account". The issuer + signing key + lifetime checks
                    // remain — those son los que importan para seguridad.
                    // Si queremos endurecerlo, lo correcto es agregar un
                    // audience-mapper al client umbral-frontend que emita
                    // "umbral-backend" o "account" en `aud`, y aqui poner
                    // ValidAudiences = new[] { audience, ... }.
                    ValidateAudience         = false,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    NameClaimType            = "preferred_username",
                    RoleClaimType            = "umbral_role",
                };

                // Keycloak nests realm roles under realm_access.roles. We flatten
                // them into "umbral_role" claims so [Authorize(Roles = "admin")]
                // works without custom requirement handlers.
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ctx =>
                    {
                        if (ctx.Principal?.Identity is not System.Security.Claims.ClaimsIdentity identity)
                            return Task.CompletedTask;

                        var realmAccess = ctx.Principal.FindFirst("realm_access");
                        if (realmAccess is null) return Task.CompletedTask;

                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(realmAccess.Value);
                            if (doc.RootElement.TryGetProperty("roles", out var roles)
                                && roles.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                foreach (var role in roles.EnumerateArray())
                                {
                                    var name = role.GetString();
                                    if (!string.IsNullOrWhiteSpace(name))
                                        identity.AddClaim(new System.Security.Claims.Claim("umbral_role", name));
                                }
                            }
                        }
                        catch { /* malformed claim — ignore */ }

                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization();
        return services;
    }
}
