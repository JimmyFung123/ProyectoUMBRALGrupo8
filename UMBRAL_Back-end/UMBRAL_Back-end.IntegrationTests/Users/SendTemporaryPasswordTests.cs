extern alias UserServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Users;

using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using UserServiceAssembly::UserService.Adapter.Controllers;
using UserServiceAssembly::UserService.Domain.Users;
using Xunit;

/// <summary>
/// <c>POST /api/users/{id}/send-temporary-password</c> had zero coverage: resets the
/// password in real Keycloak (<c>KeycloakAdminClient.ResetPasswordAsync</c>, also
/// previously untested) then hands off to <c>IUserEmailSender</c> — swapped for
/// <c>NoOpUserEmailSender</c> in this factory (see <c>UserServiceApiFactory.cs</c>),
/// so this exercises the real Keycloak reset without needing a live SMTP server.
/// </summary>
[Collection(UserServiceKeycloakCollection.Name)]
public class SendTemporaryPasswordTests(UserServiceKeycloakFixture fixture)
{
    [Fact]
    public async Task SendTemporaryPassword_ExistingUser_ReturnsNoContent()
    {
        var client = fixture.Factory.CreateClient();
        var email = $"temp-pw-{Guid.NewGuid():N}@umbral.local";
        var createResponse = await client.PostAsJsonAsync(
            "/api/users", new CreateUserRequest(email, "Temporal", "Contraseña", UserRole.Operator));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedUserResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var response = await client.PostAsync($"/api/users/{created!.Id}/send-temporary-password", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: $"unexpected status; body was: {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task SendTemporaryPassword_UnknownUser_ReturnsNotFound()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/users/{Guid.NewGuid()}/send-temporary-password", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private record CreatedUserResponse(Guid Id);
}
