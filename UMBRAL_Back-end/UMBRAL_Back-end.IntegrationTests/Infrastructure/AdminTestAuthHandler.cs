namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Admin-role stand-in JwtBearer replacement for UserService's Keycloak
/// integration tests (HU-23, UsersController — <c>[Authorize(Roles = "admin")]</c>).
///
/// The shared <see cref="TestAuthHandler"/> (used by SessionServiceApiFactory) only
/// ever carries an "operator" <c>umbral_role</c> claim and builds its
/// <see cref="ClaimsIdentity"/> with the default role claim type
/// (<see cref="ClaimTypes.Role"/>) — fine for the plain <c>[Authorize]</c> gates it
/// was built for, but useless here: UsersController requires the "admin" role, and
/// role checks resolve via <c>ClaimsPrincipal.IsInRole</c>, which only looks at
/// claims whose type matches the identity's <c>RoleType</c>. UMBRAL's real JwtBearer
/// config sets <c>RoleClaimType = "umbral_role"</c> (see
/// <c>UMBRAL.Auth.UmbralAuthExtensions</c>), so this handler mirrors that by
/// constructing the <see cref="ClaimsIdentity"/> with <c>roleType: "umbral_role"</c>
/// explicitly and an "admin" claim value.
/// </summary>
public class AdminTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AdminTestScheme";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("name", "Admin de Prueba"),
            new Claim("preferred_username", "test-admin"),
            new Claim("email", "test-admin@umbral.local"),
            new Claim("umbral_role", "admin"),
        };
        var identity = new ClaimsIdentity(
            claims, SchemeName, nameType: "preferred_username", roleType: "umbral_role");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
