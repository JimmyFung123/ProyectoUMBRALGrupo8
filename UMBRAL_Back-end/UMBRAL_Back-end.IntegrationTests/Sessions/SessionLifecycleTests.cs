// This file touches types that live in SessionService's aliased assembly (see the
// .csproj comment on the SessionService ProjectReference for why the alias exists).
extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Sessions;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using SessionServiceAssembly::SessionService.Adapter.Controllers;
using SessionServiceAssembly::SessionService.Application.Sessions;
using SessionServiceAssembly::SessionService.Domain.Sessions;
using SessionServiceAssembly::SessionService.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// Gap #6 — exercises the 17 previously zero-coverage SessionsController actions.
/// Covers the command side (state-transition + team-operation actions); the read-only
/// actions live in <see cref="SessionQueryTests"/> and the evidence actions
/// (SubmitTriviaAnswer/ValidateQr) live in <see cref="SessionEvidenceTests"/>.
///
/// SessionsController's cross-service calls (ITeamServiceClient, IStageServiceClient) are
/// clean DI-injected interfaces — no HTTP stub needed, just a DI swap per test via
/// <see cref="ClientWithFakes"/> (same technique as SessionOpsControllersTests'
/// AdminClient() override).
/// </summary>
[Collection(SessionServiceCollection.Name)]
public class SessionLifecycleTests(SessionServicePostgresFixture fixture) : IAsyncLifetime
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

    private async Task<Guid> SeedSessionAsync(SessionStatus status)
    {
        var session = Session.Create(Guid.NewGuid(), $"Sesión {Guid.NewGuid()}").Value;
        if (status is SessionStatus.InProgress or SessionStatus.Paused or SessionStatus.Completed)
            session.Start();
        if (status is SessionStatus.Paused)
            session.Pause();
        if (status is SessionStatus.Completed)
            session.Finalize();

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task<SessionStatus> StatusOfAsync(Guid sessionId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
        return (await db.Sessions.SingleAsync(s => s.Id == sessionId)).Status;
    }

    private static async Task<string> Body(HttpResponseMessage r)
        => $"status inesperado; body: {await r.Content.ReadAsStringAsync()}";

    // ── PATCH /api/sessions/{id}/start ──────────────────────────────────────────

    [Fact]
    public async Task Start_PendingSessionWithEnrolledTeams_TransitionsToInProgress()
    {
        var sessionId = await SeedSessionAsync(SessionStatus.Pending);
        using var client = ClientWithFakes();

        var response = await client.PatchAsync($"/api/sessions/{sessionId}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
        (await StatusOfAsync(sessionId)).Should().Be(SessionStatus.InProgress);
    }

    [Fact]
    public async Task Start_NoEnrolledTeams_ReturnsBadRequest()
    {
        var sessionId = await SeedSessionAsync(SessionStatus.Pending);
        using var client = ClientWithFakes(team: new FakeTeamServiceClient { HasEnrolledTeams = false });

        var response = await client.PatchAsync($"/api/sessions/{sessionId}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Start_UnknownId_ReturnsNotFound()
    {
        using var client = ClientWithFakes();

        var response = await client.PatchAsync($"/api/sessions/{Guid.NewGuid()}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PATCH /api/sessions/{id}/pause ──────────────────────────────────────────

    [Fact]
    public async Task Pause_InProgressSession_TransitionsToPaused()
    {
        var sessionId = await SeedSessionAsync(SessionStatus.InProgress);
        using var client = ClientWithFakes();

        var response = await client.PatchAsync($"/api/sessions/{sessionId}/pause", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
        (await StatusOfAsync(sessionId)).Should().Be(SessionStatus.Paused);
    }

    [Fact]
    public async Task Pause_PendingSession_ReturnsBadRequest()
    {
        var sessionId = await SeedSessionAsync(SessionStatus.Pending);
        using var client = ClientWithFakes();

        var response = await client.PatchAsync($"/api/sessions/{sessionId}/pause", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PATCH /api/sessions/{id}/resume ─────────────────────────────────────────

    [Fact]
    public async Task Resume_PausedSession_TransitionsToInProgress()
    {
        var sessionId = await SeedSessionAsync(SessionStatus.Paused);
        using var client = ClientWithFakes();

        var response = await client.PatchAsync($"/api/sessions/{sessionId}/resume", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
        (await StatusOfAsync(sessionId)).Should().Be(SessionStatus.InProgress);
    }

    [Fact]
    public async Task Resume_PendingSession_ReturnsBadRequest()
    {
        var sessionId = await SeedSessionAsync(SessionStatus.Pending);
        using var client = ClientWithFakes();

        var response = await client.PatchAsync($"/api/sessions/{sessionId}/resume", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PATCH /api/sessions/{id}/finalize ───────────────────────────────────────

    [Fact]
    public async Task Finalize_InProgressSession_TransitionsToCompleted()
    {
        var sessionId = await SeedSessionAsync(SessionStatus.InProgress);
        using var client = ClientWithFakes();

        var response = await client.PatchAsync($"/api/sessions/{sessionId}/finalize", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
        (await StatusOfAsync(sessionId)).Should().Be(SessionStatus.Completed);
    }

    [Fact]
    public async Task Finalize_PendingSession_ReturnsBadRequest()
    {
        var sessionId = await SeedSessionAsync(SessionStatus.Pending);
        using var client = ClientWithFakes();

        var response = await client.PatchAsync($"/api/sessions/{sessionId}/finalize", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Finalize_UnknownId_ReturnsNotFound()
    {
        using var client = ClientWithFakes();

        var response = await client.PatchAsync($"/api/sessions/{Guid.NewGuid()}/finalize", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/sessions/{id}/broadcast-message ───────────────────────────────

    [Fact]
    public async Task BroadcastOperatorMessage_InProgressSession_ReturnsOk()
    {
        var sessionId = await SeedSessionAsync(SessionStatus.InProgress);
        using var client = ClientWithFakes();

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/broadcast-message", new BroadcastOperatorMessageRequest("Atención equipos"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }

    [Fact]
    public async Task BroadcastOperatorMessage_EmptyMessage_ReturnsBadRequest()
    {
        var sessionId = await SeedSessionAsync(SessionStatus.InProgress);
        using var client = ClientWithFakes();

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/broadcast-message", new BroadcastOperatorMessageRequest("   "));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BroadcastOperatorMessage_UnknownSession_ReturnsNotFound()
    {
        using var client = ClientWithFakes();

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{Guid.NewGuid()}/broadcast-message", new BroadcastOperatorMessageRequest("Hola"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/sessions/{id}/teams/{teamId}/release-clue ─────────────────────

    [Fact]
    public async Task ReleaseClue_InProgressSession_ReturnsOk()
    {
        var sessionId = await SeedSessionAsync(SessionStatus.InProgress);
        using var client = ClientWithFakes();

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/teams/{Guid.NewGuid()}/release-clue",
            new ReleaseClueRequest(TotalCluesForStage: 3, ClueContent: "Buscá bajo el reloj"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }

    [Fact]
    public async Task ReleaseClue_SessionNotInProgress_ReturnsBadRequest()
    {
        var sessionId = await SeedSessionAsync(SessionStatus.Pending);
        using var client = ClientWithFakes();

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/teams/{Guid.NewGuid()}/release-clue",
            new ReleaseClueRequest(TotalCluesForStage: 3));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReleaseClue_AllCluesAlreadyReleased_ReturnsConflict()
    {
        var sessionId = await SeedSessionAsync(SessionStatus.InProgress);
        using var client = ClientWithFakes(team: new FakeTeamServiceClient { ReleaseClueResult = -1 });

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/teams/{Guid.NewGuid()}/release-clue",
            new ReleaseClueRequest(TotalCluesForStage: 3));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ReleaseClue_UnknownSession_ReturnsNotFound()
    {
        using var client = ClientWithFakes();

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{Guid.NewGuid()}/teams/{Guid.NewGuid()}/release-clue",
            new ReleaseClueRequest(TotalCluesForStage: 3));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/sessions/{id}/teams/{teamId}/penalize ──────────────────────────

    [Fact]
    public async Task PenalizeTeam_InProgressSession_ReturnsOk()
    {
        var sessionId = await SeedSessionAsync(SessionStatus.InProgress);
        using var client = ClientWithFakes();

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/teams/{Guid.NewGuid()}/penalize",
            new PenalizeTeamRequest(Points: 5, Reason: "Uso de celular"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }

    [Fact]
    public async Task PenalizeTeam_UnknownSession_ReturnsNotFound()
    {
        using var client = ClientWithFakes();

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{Guid.NewGuid()}/teams/{Guid.NewGuid()}/penalize",
            new PenalizeTeamRequest(Points: 5, Reason: "x"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/sessions/{id}/teams/{teamId}/force-advance ─────────────────────

    [Fact]
    public async Task ForceAdvanceTeam_TeamOnFirstStageWithMoreStagesLeft_ReturnsOk()
    {
        var sessionId = await SeedSessionAsync(SessionStatus.InProgress);
        var teamId = Guid.NewGuid();
        var team = new TeamProgressItem(teamId, "Equipo Alfa", CurrentStageOrder: 1, CluesReceivedCurrentStage: 0, ClueTimerResetAt: null, LastClueWasAutomatic: false);
        var stages = new List<StageInfo> { new(Guid.NewGuid(), Order: 1), new(Guid.NewGuid(), Order: 2) };

        using var client = ClientWithFakes(
            team: new FakeTeamServiceClient { TeamProgressResult = [team] },
            stage: new FakeStageServiceClient { StagesResult = stages });

        var response = await client.PostAsync($"/api/sessions/{sessionId}/teams/{teamId}/force-advance", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }

    [Fact]
    public async Task ForceAdvanceTeam_TeamNotEnrolled_ReturnsBadRequest()
    {
        var sessionId = await SeedSessionAsync(SessionStatus.InProgress);
        using var client = ClientWithFakes(); // TeamProgressResult defaults to empty

        var response = await client.PostAsync($"/api/sessions/{sessionId}/teams/{Guid.NewGuid()}/force-advance", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ForceAdvanceTeam_AlreadyOnLastStage_ReturnsConflict()
    {
        var sessionId = await SeedSessionAsync(SessionStatus.InProgress);
        var teamId = Guid.NewGuid();
        var team = new TeamProgressItem(teamId, "Equipo Alfa", CurrentStageOrder: 2, CluesReceivedCurrentStage: 0, ClueTimerResetAt: null, LastClueWasAutomatic: false);
        var stages = new List<StageInfo> { new(Guid.NewGuid(), Order: 1), new(Guid.NewGuid(), Order: 2) };

        using var client = ClientWithFakes(
            team: new FakeTeamServiceClient { TeamProgressResult = [team] },
            stage: new FakeStageServiceClient { StagesResult = stages });

        var response = await client.PostAsync($"/api/sessions/{sessionId}/teams/{teamId}/force-advance", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ForceAdvanceTeam_UnknownSession_ReturnsNotFound()
    {
        using var client = ClientWithFakes();

        var response = await client.PostAsync($"/api/sessions/{Guid.NewGuid()}/teams/{Guid.NewGuid()}/force-advance", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/sessions/{id}/teams/{teamId}/leave (AllowAnonymous) ───────────

    [Fact]
    public async Task LeaveTeam_AlwaysForwardsToTeamServiceAndReturnsOk()
    {
        // El handler no valida la sesión — solo reenvía a TeamService (best-effort);
        // por eso alcanza con IDs aleatorios para ejercitar la acción completa.
        using var client = ClientWithFakes();

        var response = await client.PostAsync(
            $"/api/sessions/{Guid.NewGuid()}/teams/{Guid.NewGuid()}/leave", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }
}
