// This file touches types that live in UserService's aliased assembly (see the
// .csproj comment on the UserService ProjectReference for why the alias exists).
extern alias UserServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Users;

using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using UserServiceAssembly::UserService.Adapter.Controllers;
using UserServiceAssembly::UserService.Application.Users;
using UserServiceAssembly::UserService.Domain.Users;
using Xunit;

/// <summary>
/// HU-23 — blinds the real-Keycloak regression documented in
/// <c>KeycloakAdminClient.cs</c>: .NET's default record serialization emits
/// PascalCase (<c>{"Id":...,"Name":...}</c>), which Keycloak's Jackson-based Admin
/// API silently ignores on <c>POST /role-mappings/realm</c> — the call returns 2xx
/// but never assigns the role, leaving the user orphaned (what happened with
/// prueba@umbral.local in production). <c>KeycloakAdminClient</c> now forces
/// <c>JsonNamingPolicy.CamelCase</c>, but that fix was never verified against a real
/// Keycloak — a mocked <c>IKeycloakAdminClient</c> can't catch it, because the bug is
/// specific to how the REAL Keycloak deserializes the request body.
///
/// These tests drive <see cref="UsersController"/> over real HTTP (exactly what the
/// front-end does) and then verify the RESULT against the real Keycloak Admin API
/// (via the same <see cref="IKeycloakAdminClient"/> the production code uses) —
/// not against an in-memory double.
/// </summary>
[Collection(UserServiceKeycloakCollection.Name)]
public class UserKeycloakIntegrationTests(UserServiceKeycloakFixture fixture)
{
    [Fact]
    public async Task CreateUser_ValidRequestThroughHttp_ExistsInRealKeycloakWithCorrectRole()
    {
        var client = fixture.Factory.CreateClient();
        var email = $"pilot-{Guid.NewGuid():N}@umbral.local";
        var request = new CreateUserRequest(email, "Piloto", "De Prueba", UserRole.Admin);

        var response = await client.PostAsJsonAsync("/api/users", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            because: await SafeReadBody(response));

        var body = await response.Content.ReadFromJsonAsync<CreatedUserResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        body.Should().NotBeNull();
        body!.Id.Should().NotBeEmpty();

        // Verify against the REAL Keycloak Admin API — not a mock. This is exactly
        // the check a PascalCase-serialized role-mappings request would fail: the
        // user would exist (2xx from POST /users) but GetUserRoleAsync would come
        // back null because the role-assignment POST was silently ignored.
        using var scope = fixture.Factory.Services.CreateScope();
        var keycloak = scope.ServiceProvider.GetRequiredService<IKeycloakAdminClient>();

        var keycloakUser = await keycloak.GetByIdAsync(body.Id, CancellationToken.None);

        keycloakUser.Should().NotBeNull();
        keycloakUser!.Email.Should().Be(email);
        keycloakUser.Enabled.Should().BeTrue();
        keycloakUser.Role.Should().Be(UserRole.Admin,
            because: "a PascalCase-serialized role-mappings body would leave this null " +
                     "(the exact regression this suite exists to catch)");
    }

    [Fact]
    public async Task ChangeRole_OperatorPromotedToAdminThroughHttp_OldRoleRemovedAndNewRoleAssignedInRealKeycloak()
    {
        var client = fixture.Factory.CreateClient();
        var email = $"promote-{Guid.NewGuid():N}@umbral.local";
        var createRequest = new CreateUserRequest(email, "Promovido", "De Prueba", UserRole.Operator);

        var createResponse = await client.PostAsJsonAsync("/api/users", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            because: await SafeReadBody(createResponse));
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedUserResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        using var scope = fixture.Factory.Services.CreateScope();
        var keycloak = scope.ServiceProvider.GetRequiredService<IKeycloakAdminClient>();

        (await keycloak.GetByIdAsync(created!.Id, CancellationToken.None))!
            .Role.Should().Be(UserRole.Operator);

        var changeRoleResponse = await client.PutAsJsonAsync(
            $"/api/users/{created.Id}/role", new ChangeRoleRequest(UserRole.Admin));

        changeRoleResponse.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: await SafeReadBody(changeRoleResponse));

        var afterChange = await keycloak.GetByIdAsync(created.Id, CancellationToken.None);
        afterChange.Should().NotBeNull();
        afterChange!.Role.Should().Be(UserRole.Admin,
            because: "ChangeRoleAsync must assign the new role AND remove the old one " +
                     "against a real Keycloak, not just an in-memory double");
    }

    private static async Task<string> SafeReadBody(HttpResponseMessage response)
        => $"unexpected status; body was: {await response.Content.ReadAsStringAsync()}";

    private record CreatedUserResponse(Guid Id);
}
