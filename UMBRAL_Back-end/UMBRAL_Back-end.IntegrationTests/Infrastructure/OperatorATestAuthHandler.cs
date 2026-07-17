namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// RB-10 ownership tests need two distinct real operator identities — the shared
/// <see cref="TestAuthHandler"/> carries no "sub" claim at all, so every request
/// through it maps to <c>CreatedByOperatorId == null</c> (treated as unowned by
/// <c>SessionOwnershipFilter</c>), which can never reproduce a cross-operator
/// denial. This handler is otherwise identical to <see cref="TestAuthHandler"/>.
/// </summary>
public class OperatorATestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "OperatorATestScheme";
    public const string Sub = "test-operator-a-sub";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("name", "Operador A"),
            new Claim("preferred_username", "operator-a"),
            new Claim("umbral_role", "operator"),
            new Claim("sub", Sub),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
