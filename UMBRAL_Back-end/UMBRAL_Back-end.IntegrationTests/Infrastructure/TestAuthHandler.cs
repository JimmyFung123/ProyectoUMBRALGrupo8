namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Stand-in JwtBearer replacement for Level-1 tests against services that gate
/// endpoints with <c>[Authorize]</c> (HU-23, e.g. SessionService's SessionsController).
/// Real tests would need a live Keycloak token, which Testcontainers-only Level-1
/// tests do not have; this handler authenticates every request unconditionally
/// with a fixed operator identity so [Authorize] passes and the persistence path
/// under test (not the auth pipeline) is what gets exercised.
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestScheme";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("name", "Operador de Prueba"),
            new Claim("preferred_username", "test-operator"),
            new Claim("umbral_role", "operator"),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
