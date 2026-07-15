namespace UMBRAL_Back_end.IntegrationTests.Users;

using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FluentAssertions;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using Xunit;

/// <summary>
/// Exercises <c>UMBRAL.Auth.UmbralAuthExtensions.AddUmbralJwtAuth</c> — in
/// particular its <c>OnTokenValidated</c> handler, which manually parses the
/// <c>realm_access</c> claim (a JSON string) and flattens each nested role into a
/// <c>Claim("umbral_role", ...)</c> so <c>[Authorize(Roles = "admin")]</c> works —
/// against a REAL Keycloak-signed JWT for the first time. Every other suite that
/// touches auth (<see cref="AdminTestAuthHandler"/>, <see cref="TestAuthHandler"/>)
/// bypasses JwtBearer entirely and fabricates the already-flattened
/// <c>umbral_role</c> claim by hand, so none of them could ever catch a regression
/// in the parsing logic itself (e.g. the silent <c>catch { }</c> swallowing a
/// malformed/missing claim and leaving the caller authenticated with zero roles).
///
/// Uses <see cref="UserServiceJwtFixture"/>, which boots a real Testcontainers
/// Keycloak from a test-only realm copy with a ROPC-enabled client added (see that
/// fixture's doc comment) — the real realm has no client that supports the
/// "password" grant, so this is the only way to get a real user token in a test.
/// </summary>
[Collection(UserServiceJwtCollection.Name)]
public class UserJwtValidationTests(UserServiceJwtFixture fixture)
{
    [Fact]
    public async Task GetAll_RealAdminToken_PassesRealValidationAndRoleFlattening_Returns200()
    {
        var token = await fixture.GetAccessTokenAsync(
            UserServiceJwtFixture.AdminUsername, UserServiceJwtFixture.AdminPassword);

        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/users");

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "a real admin@umbral.local token should pass signature/issuer " +
                     $"validation and have its realm_access.roles flattened into " +
                     $"umbral_role=admin by OnTokenValidated; response body was: {body}");
    }

    [Fact]
    public async Task GetAll_RealOperatorOnlyToken_FailsAdminRoleCheck_Returns403()
    {
        var token = await fixture.GetAccessTokenAsync(
            UserServiceJwtFixture.OperatorUsername, UserServiceJwtFixture.OperatorPassword);

        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "operator-test@umbral.local only has the 'operator' realm role, so " +
                     "after OnTokenValidated flattens it to umbral_role=operator, " +
                     "[Authorize(Roles = \"admin\")] on UsersController must reject it — " +
                     "proving both that real-role flattening works AND that it is actually " +
                     "enforced, not just fabricated claims passing through unchecked");
    }
}
