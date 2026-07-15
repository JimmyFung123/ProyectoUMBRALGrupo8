// This file touches types that live in SessionService's aliased assembly (see the
// .csproj comment on the SessionService ProjectReference for why the alias exists).
extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Sessions;

using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using SessionServiceAssembly::SessionService.Application.Sessions;
using SessionServiceAssembly::SessionService.Domain.Sessions;
using SessionServiceAssembly::SessionService.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// Gap #6 — read-only SessionsController actions with zero prior coverage:
/// GetAudit, GetCommandAudit, GetDashboard, GetRanking, GetParticipantStage,
/// GetReleasedClues. GetAudit/GetCommandAudit/GetDashboard are pure local reads
/// (SessionEvents table) so they need only a seeded Session; the other three go
/// through ITeamServiceClient/IStageServiceClient — swapped for fakes, same as
/// <see cref="SessionLifecycleTests"/>.
/// </summary>
[Collection(SessionServiceCollection.Name)]
public class SessionQueryTests(SessionServicePostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    // SessionServiceApiFactory already registers TestAuthHandler as the default auth
    // scheme (base ConfigureWebHost) — no need to re-register it here, only swap the
    // cross-service clients.
    private HttpClient ClientWithFakes(FakeTeamServiceClient? team = null, FakeStageServiceClient? stage = null)
        => fixture.Factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.RemoveAll<ITeamServiceClient>();
            services.AddSingleton<ITeamServiceClient>(team ?? new FakeTeamServiceClient());

            services.RemoveAll<IStageServiceClient>();
            services.AddSingleton<IStageServiceClient>(stage ?? new FakeStageServiceClient());
        })).CreateClient();

    private async Task<Guid> SeedPendingSessionAsync()
    {
        var session = Session.Create(Guid.NewGuid(), $"Sesión {Guid.NewGuid()}").Value;

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private static async Task<string> Body(HttpResponseMessage r)
        => $"status inesperado; body: {await r.Content.ReadAsStringAsync()}";

    // ── GET /api/sessions/{id}/audit ────────────────────────────────────────────

    [Fact]
    public async Task GetAudit_ExistingSession_ReturnsEmptyTimeline()
    {
        var sessionId = await SeedPendingSessionAsync();
        using var client = ClientWithFakes();

        var response = await client.GetAsync($"/api/sessions/{sessionId}/audit");

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }

    [Fact]
    public async Task GetAudit_UnknownSession_ReturnsNotFound()
    {
        using var client = ClientWithFakes();

        var response = await client.GetAsync($"/api/sessions/{Guid.NewGuid()}/audit");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/sessions/{id}/audit-log ────────────────────────────────────────

    [Fact]
    public async Task GetCommandAudit_ExistingSession_ReturnsOk()
    {
        var sessionId = await SeedPendingSessionAsync();
        using var client = ClientWithFakes();

        var response = await client.GetAsync($"/api/sessions/{sessionId}/audit-log");

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }

    [Fact]
    public async Task GetCommandAudit_UnknownSession_ReturnsNotFound()
    {
        using var client = ClientWithFakes();

        var response = await client.GetAsync($"/api/sessions/{Guid.NewGuid()}/audit-log");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/sessions/{id}/dashboard ────────────────────────────────────────

    [Fact]
    public async Task GetDashboard_ExistingSession_ReturnsOk()
    {
        var sessionId = await SeedPendingSessionAsync();
        using var client = ClientWithFakes();

        var response = await client.GetAsync($"/api/sessions/{sessionId}/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }

    [Fact]
    public async Task GetDashboard_UnknownSession_ReturnsNotFound()
    {
        using var client = ClientWithFakes();

        var response = await client.GetAsync($"/api/sessions/{Guid.NewGuid()}/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/sessions/{id}/ranking (AllowAnonymous) ──────────────────────────

    [Fact]
    public async Task GetRanking_ExistingSession_ReturnsOk()
    {
        var sessionId = await SeedPendingSessionAsync();
        using var client = ClientWithFakes();

        var response = await client.GetAsync($"/api/sessions/{sessionId}/ranking");

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }

    [Fact]
    public async Task GetRanking_UnknownSession_ReturnsNotFound()
    {
        using var client = ClientWithFakes();

        var response = await client.GetAsync($"/api/sessions/{Guid.NewGuid()}/ranking");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/sessions/{id}/participant-stage/{teamId} (AllowAnonymous) ──────

    [Fact]
    public async Task GetParticipantStage_TeamNotEnrolled_ReturnsBadRequest()
    {
        var sessionId = await SeedPendingSessionAsync();
        using var client = ClientWithFakes(); // TeamInfoResult defaults to null → TeamNotFound

        var response = await client.GetAsync($"/api/sessions/{sessionId}/participant-stage/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetParticipantStage_TeamNotYetStarted_ReturnsWaitingStatus()
    {
        var sessionId = await SeedPendingSessionAsync();
        var teamId = Guid.NewGuid();
        var team = new TeamInfoItem(teamId, "Equipo Alfa", CurrentStageOrder: 0);
        using var client = ClientWithFakes(team: new FakeTeamServiceClient { TeamInfoResult = team });

        var response = await client.GetAsync($"/api/sessions/{sessionId}/participant-stage/{teamId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }

    [Fact]
    public async Task GetParticipantStage_UnknownSession_ReturnsNotFound()
    {
        using var client = ClientWithFakes();

        var response = await client.GetAsync($"/api/sessions/{Guid.NewGuid()}/participant-stage/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/sessions/{id}/teams/{teamId}/released-clues (AllowAnonymous) ──

    [Fact]
    public async Task GetReleasedClues_TeamNotEnrolled_ReturnsBadRequest()
    {
        var sessionId = await SeedPendingSessionAsync();
        using var client = ClientWithFakes(); // TeamProgressResult defaults to empty → TeamNotFound

        var response = await client.GetAsync($"/api/sessions/{sessionId}/teams/{Guid.NewGuid()}/released-clues");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetReleasedClues_TeamNotYetStarted_ReturnsWaitingSentinel()
    {
        var sessionId = await SeedPendingSessionAsync();
        var teamId = Guid.NewGuid();
        var team = new TeamProgressItem(teamId, "Equipo Alfa", CurrentStageOrder: 0, CluesReceivedCurrentStage: 0, ClueTimerResetAt: null, LastClueWasAutomatic: false);
        using var client = ClientWithFakes(team: new FakeTeamServiceClient { TeamProgressResult = [team] });

        var response = await client.GetAsync($"/api/sessions/{sessionId}/teams/{teamId}/released-clues");

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }

    [Fact]
    public async Task GetReleasedClues_UnknownSession_ReturnsNotFound()
    {
        using var client = ClientWithFakes();

        var response = await client.GetAsync($"/api/sessions/{Guid.NewGuid()}/teams/{Guid.NewGuid()}/released-clues");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
