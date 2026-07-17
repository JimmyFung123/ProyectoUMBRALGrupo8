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
///     "Authority": "http://localhost:18090/realms/umbral",
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
            ?? "http://localhost:18090/realms/umbral";
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
                // Keycloak en Docker: el navegador obtiene tokens con issuer =
                // URL pública (p.ej. https://kc.dominio), pero los servicios deben
                // bajar metadata/JWKS por la red interna. Si se define
                // Keycloak__MetadataAddress, el fetch va por ahí mientras el
                // issuer validado sigue siendo Authority. (Requiere en Keycloak
                // KC_HOSTNAME fijo + KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true.)
                var metadataAddress = configuration["Keycloak:MetadataAddress"];
                if (!string.IsNullOrWhiteSpace(metadataAddress))
                    options.MetadataAddress = metadataAddress;
                // Dev corre Keycloak por HTTP plano, así que por defecto es false.
                // En prod (Keycloak detrás de HTTPS) ponelo en true con la env var
                // Keycloak__RequireHttpsMetadata=true.
                options.RequireHttpsMetadata =
                    configuration.GetValue<bool?>("Keycloak:RequireHttpsMetadata") ?? false;
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

    /// <summary>Nombre de la política CORS que usan todos los servicios UMBRAL.</summary>
    public const string CorsPolicy = "AllowFrontend";

    /// <summary>
    /// Registra una política CORS unificada para todos los servicios. Permite:
    ///  • los orígenes públicos exactos listados en <c>Cors:AllowedOrigins</c>
    ///    (prod: las URLs de los fronts detrás del túnel), y
    ///  • cualquier loopback/LAN en los puertos de Vite (5173/5174), para que en
    ///    dev los participantes entren desde el celular en la misma red.
    /// Mantiene <c>AllowCredentials</c> (necesario para SignalR), por eso refleja
    /// el origen concreto en vez de usar <c>AllowAnyOrigin</c>.
    /// </summary>
    public static IServiceCollection AddUmbralCors(this IServiceCollection services, IConfiguration configuration)
    {
        var configured = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                         ?? Array.Empty<string>();

        services.AddCors(options =>
            options.AddPolicy(CorsPolicy, policy =>
                policy.SetIsOriginAllowed(origin => IsAllowedOrigin(origin, configured))
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials()));

        return services;
    }

    private static bool IsAllowedOrigin(string origin, string[] configured)
    {
        // Prod: coincidencia exacta con un origen configurado.
        foreach (var c in configured)
            if (string.Equals(c, origin, StringComparison.OrdinalIgnoreCase))
                return true;

        // Dev: loopback o IP privada en los puertos de Vite.
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
        if (uri.Port != 5173 && uri.Port != 5174) return false;
        return uri.IsLoopback
            || uri.Host.StartsWith("192.168.")
            || uri.Host.StartsWith("10.")
            || uri.Host.StartsWith("172.");
    }
}
