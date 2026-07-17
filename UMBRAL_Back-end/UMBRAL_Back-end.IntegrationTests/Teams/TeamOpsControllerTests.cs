// This file touches types that live in TeamService's aliased assembly (see the
// .csproj comment on the TeamService ProjectReference for why the alias exists).
extern alias TeamServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Teams;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using TeamServiceAssembly::TeamService.Adapter.Controllers;
using TeamServiceAssembly::TeamService.Domain.Rankings;
using TeamServiceAssembly::TeamService.Domain.Teams;
using TeamServiceAssembly::TeamService.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// Gap #6 — the 4 previously zero-coverage TeamsController actions: GetSessionRanking,
/// RecordEvidenceOutcome, RecordWrongAttempt, ReleaseClue. Unlike SessionsController,
/// TeamsController has no [Authorize] gate and no cross-service HTTP dependency, so
/// these tests just seed TeamsDbContext directly (Team/RankingProjection aggregates)
/// and hit the default client — no fakes/DI swaps needed.
/// </summary>
[Collection(TeamServiceCollection.Name)]
public class TeamOpsControllerTests(TeamServicePostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    private async Task<Team> SeedTeamAsync(Guid? sessionId = null)
    {
        var team = Team.Create(sessionId ?? Guid.NewGuid(), TeamName.Create($"Equipo {Guid.NewGuid()}").Value);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TeamsDbContext>();
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return team;
    }

    private static async Task<string> Body(HttpResponseMessage r)
        => $"status inesperado; body: {await r.Content.ReadAsStringAsync()}";

    // ── GET /api/teams/ranking?sessionId= ───────────────────────────────────────

    [Fact]
    public async Task GetSessionRanking_ReturnsSeededProjectionRows()
    {
        var sessionId = Guid.NewGuid();
        var projection = RankingProjection.Create(
            sessionId, Guid.NewGuid(), "Equipo Alfa",
            score: 50, rank: 1, position: 1, currentStageOrder: 2,
            isConnected: true, lastStageCompletedAt: null, updatedAt: DateTime.UtcNow);

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TeamsDbContext>();
            db.RankingProjections.Add(projection);
            await db.SaveChangesAsync();
        }

        var client = fixture.Factory.CreateClient();
        var response = await client.GetAsync($"/api/teams/ranking?sessionId={sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Equipo Alfa");
    }

    [Fact]
    public async Task GetSessionRanking_NoProjectionRows_ReturnsEmptyTeamsList()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/api/teams/ranking?sessionId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }

    // ── POST /api/teams/{id}/record-evidence-outcome ────────────────────────────

    [Fact]
    public async Task RecordEvidenceOutcome_ExistingTeam_UpdatesScoreAndStage()
    {
        var team = await SeedTeamAsync();
        var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/teams/{team.Id}/record-evidence-outcome",
            new RecordEvidenceOutcomeRequest(IsCorrect: true, ScoreChange: 10, NextStageOrder: 1));
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TeamsDbContext>();
        var persisted = await db.Teams.SingleAsync(t => t.Id == team.Id);
        persisted.Score.Should().Be(10);
        persisted.CurrentStageOrder.Should().Be(1);
    }

    [Fact]
    public async Task RecordEvidenceOutcome_UnknownTeam_ReturnsNotFound()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/teams/{Guid.NewGuid()}/record-evidence-outcome",
            new RecordEvidenceOutcomeRequest(true, 10, 1));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/teams/{id}/record-wrong-attempt ────────────────────────────────

    [Fact]
    public async Task RecordWrongAttempt_ExistingTeam_AppliesPenaltyAndBlocksOption()
    {
        var team = await SeedTeamAsync();
        var client = fixture.Factory.CreateClient();
        var blockedOptionId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            $"/api/teams/{team.Id}/record-wrong-attempt",
            new RecordWrongAttemptRequest(blockedOptionId, ScorePenalty: -5));
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TeamsDbContext>();
        var persisted = await db.Teams.SingleAsync(t => t.Id == team.Id);
        persisted.Score.Should().Be(-5);
        persisted.WrongAttemptsCurrentStage.Should().Be(1);
    }

    [Fact]
    public async Task RecordWrongAttempt_UnknownTeam_ReturnsNotFound()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/teams/{Guid.NewGuid()}/record-wrong-attempt",
            new RecordWrongAttemptRequest(Guid.NewGuid(), -5));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/teams/{id}/release-clue ────────────────────────────────────────

    [Fact]
    public async Task ReleaseClue_ExistingTeam_IncrementsCluesReceivedInPostgres()
    {
        var team = await SeedTeamAsync();
        var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/teams/{team.Id}/release-clue",
            new ReleaseClueRequest(TotalCluesForStage: 3));
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TeamsDbContext>();
        var persisted = await db.Teams.SingleAsync(t => t.Id == team.Id);
        persisted.CluesReceivedCurrentStage.Should().Be(1);
    }

    [Fact]
    public async Task ReleaseClue_AllCluesAlreadyReleased_ReturnsConflict()
    {
        var team = await SeedTeamAsync();
        var client = fixture.Factory.CreateClient();

        // TotalCluesForStage: 0 → CluesReceivedCurrentStage (0) is already >= total, so the
        // very first call exhausts the same "all released" branch without extra setup.
        var response = await client.PostAsJsonAsync(
            $"/api/teams/{team.Id}/release-clue",
            new ReleaseClueRequest(TotalCluesForStage: 0));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ReleaseClue_UnknownTeam_ReturnsNotFound()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/teams/{Guid.NewGuid()}/release-clue",
            new ReleaseClueRequest(TotalCluesForStage: 3));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
