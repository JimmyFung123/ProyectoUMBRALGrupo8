namespace UMBRAL_Back_end.IntegrationTests.Sessions;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using Xunit;

/// <summary>
/// Gap #3 — controllers de SessionService que no tenían ningún test de integración:
/// MissionStructure (Composite+Visitor, [Authorize]), Internal (has-active, anónimo,
/// lo llama MissionService), Statistics y SyncHealth (ambos [Authorize(Roles=admin)]).
/// Comparten la colección/fixture de SessionService.
/// </summary>
[Collection(SessionServiceCollection.Name)]
public class SessionOpsControllersTests(SessionServicePostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // El fixture usa TestAuthHandler (rol "operator"). Para los endpoints
    // [Authorize(Roles=admin)] hace falta un principal admin: overrideamos el
    // esquema de auth con AdminTestAuthHandler (la última AddAuthentication gana).
    private HttpClient AdminClient()
        => fixture.Factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.AddAuthentication(AdminTestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, AdminTestAuthHandler>(AdminTestAuthHandler.SchemeName, _ => { });
        })).CreateClient();

    // ── MissionStructureController ──────────────────────────────────────────────

    [Fact]
    public async Task MissionStructure_UnknownMission_ReturnsNotFound()
    {
        // Sin la misión en la réplica MissionLookup, el árbol Composite no se arma:
        // MissionStructureErrors.MissionNotFound → 404 (antes de llamar a Stage/Clue).
        var response = await fixture.Factory.CreateClient()
            .GetAsync($"/api/missions/{Guid.NewGuid()}/structure");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── InternalController (lo consume MissionService para RB-15) ────────────────

    [Fact]
    public async Task Internal_HasActiveSessions_ReturnsFalseWhenNoSessions()
    {
        var response = await fixture.Factory.CreateClient()
            .GetAsync($"/api/internal/sessions/has-active?missionId={Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("hasActiveSessions").GetBoolean().Should().BeFalse();
    }

    // ── StatisticsController ([Authorize(Roles=admin)]) ─────────────────────────

    [Fact]
    public async Task Statistics_AsAdmin_ReturnsOk()
    {
        var response = await AdminClient().GetAsync("/api/statistics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Statistics_AsOperator_ReturnsForbidden()
    {
        // El cliente por defecto lleva rol "operator": el gate de admin lo rechaza.
        var response = await fixture.Factory.CreateClient().GetAsync("/api/statistics");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── SyncHealthController ([Authorize(Roles=admin)]) ─────────────────────────

    [Fact]
    public async Task SyncHealth_AsAdmin_ReturnsOk()
    {
        var response = await AdminClient().GetAsync("/api/synchealth");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
