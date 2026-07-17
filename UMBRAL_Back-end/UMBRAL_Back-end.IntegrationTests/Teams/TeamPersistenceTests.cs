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
using Npgsql;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using TeamServiceAssembly::TeamService.Adapter.Controllers;
using TeamServiceAssembly::TeamService.Application.Teams.Commands.CreateTeam;
using TeamServiceAssembly::TeamService.Domain.Teams;
using TeamServiceAssembly::TeamService.Infrastructure.Persistence;
using Xunit;

[Collection(TeamServiceCollection.Name)]
public class TeamPersistenceTests(TeamServicePostgresFixture fixture) : IAsyncLifetime
{
    // xUnit creates a new instance of this class per [Fact], so InitializeAsync runs before
    // EACH test — unlike the collection fixture, which is shared by the whole class. Resetting
    // here keeps tests isolated from rows left over by earlier tests in the same collection.
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    private async Task<CreateTeamResult> CreateTeamAsync(HttpClient client, Guid sessionId, string? name = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/teams", new CreateTeamRequest(sessionId, name ?? $"Equipo {Guid.NewGuid()}"));
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
        return (await response.Content.ReadFromJsonAsync<CreateTeamResult>(CaseInsensitive))!;
    }

    private static async Task<string> Body(HttpResponseMessage r)
        => $"status inesperado; body: {await r.Content.ReadAsStringAsync()}";

    private record NewScoreResponse(int NewScore);

    // ── Create + constraints + migración (base original) ────────────────────────

    [Fact]
    public async Task CreateTeam_ValidRequestThroughHttp_PersistsInRealPostgres()
    {
        var client = fixture.Factory.CreateClient();
        var sessionId = Guid.NewGuid();
        var request = new CreateTeamRequest(sessionId, $"Equipo Alfa {Guid.NewGuid()}");

        var response = await client.PostAsJsonAsync("/api/teams", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CreateTeamResult>(CaseInsensitive);
        body.Should().NotBeNull();

        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamsDbContext>();
        var persisted = await dbContext.Teams.SingleAsync(t => t.Id == body!.TeamId);

        persisted.SessionId.Should().Be(sessionId);
        persisted.Name.Should().Be(request.TeamName);
        persisted.InviteCode.Should().Be(body!.InviteCode);
        persisted.MemberCount.Should().Be(1);
    }

    [Fact]
    public async Task SaveChanges_DuplicateInviteCode_ThrowsDueToUniqueIndexInPostgres()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamsDbContext>();

        var sessionId = Guid.NewGuid();
        var first = Team.Create(sessionId, TeamName.Create("Equipo Uno").Value);
        var second = Team.Create(sessionId, TeamName.Create("Equipo Dos").Value);

        dbContext.Teams.Add(first);
        await dbContext.SaveChangesAsync();

        dbContext.Teams.Add(second);
        dbContext.Entry(second).Property(nameof(Team.InviteCode)).CurrentValue = first.InviteCode;

        var act = async () => await dbContext.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChanges_InviteCodeLongerThanColumnLimit_ThrowsDueToVarcharLengthInPostgres()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamsDbContext>();

        var oversizedInviteCode = new string('A', 11); // column is varchar(10)
        var id = Guid.NewGuid();

        var act = async () => await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "Teams"
                 ("Id", "SessionId", "Name", "InviteCode", "MemberCount", "IsConnected",
                  "CurrentStageOrder", "CluesReceivedCurrentStage", "TotalCluesReceived",
                  "Score", "LastClueWasAutomatic", "WrongAttemptsCurrentStage")
             VALUES
                 ({id}, {Guid.NewGuid()}, 'Equipo Sobredimensionado', {oversizedInviteCode}, 1, false,
                  0, 0, 0, 0, false, 0)
             """);

        (await act.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be("22001");
    }

    [Fact]
    public async Task Database_Migrate_AppliesAllMigrationsCleanlyAndIsIdempotent()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamsDbContext>();

        var pendingBeforeReRun = await dbContext.Database.GetPendingMigrationsAsync();
        pendingBeforeReRun.Should().BeEmpty();

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        var allMigrations = dbContext.Database.GetMigrations();
        appliedMigrations.Should().BeEquivalentTo(allMigrations);

        var act = async () => await dbContext.Database.MigrateAsync();
        await act.Should().NotThrowAsync();

        (await dbContext.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }

    // ── GET /api/teams/{id} ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetTeamById_ExistingTeam_ReturnsOk()
    {
        var client = fixture.Factory.CreateClient();
        var team = await CreateTeamAsync(client, Guid.NewGuid());

        var response = await client.GetAsync($"/api/teams/{team.TeamId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTeamById_UnknownId_ReturnsNotFound()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/api/teams/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/teams/{inviteCode}/join ───────────────────────────────────────

    [Fact]
    public async Task JoinTeam_ValidInviteCode_IncrementsMemberCountInPostgres()
    {
        var client = fixture.Factory.CreateClient();
        var team = await CreateTeamAsync(client, Guid.NewGuid());

        var response = await client.PostAsync($"/api/teams/{team.InviteCode}/join", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));

        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamsDbContext>();
        var persisted = await dbContext.Teams.SingleAsync(t => t.Id == team.TeamId);
        persisted.MemberCount.Should().Be(2);
    }

    [Fact]
    public async Task JoinTeam_UnknownInviteCode_ReturnsNotFound()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.PostAsync("/api/teams/NOEXISTE/join", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/teams?sessionId (progreso por sesión) ──────────────────────────

    [Fact]
    public async Task GetTeamProgress_ReturnsTeamsOfThatSessionOnly()
    {
        var client = fixture.Factory.CreateClient();
        var sessionId = Guid.NewGuid();
        await CreateTeamAsync(client, sessionId);
        await CreateTeamAsync(client, sessionId);
        await CreateTeamAsync(client, Guid.NewGuid()); // otra sesión — no debe aparecer

        var teams = await client.GetFromJsonAsync<List<JsonElement>>($"/api/teams?sessionId={sessionId}");

        teams.Should().NotBeNull();
        teams!.Should().HaveCount(2);
    }

    // ── POST /api/teams/{id}/leave ──────────────────────────────────────────────

    [Fact]
    public async Task LeaveTeam_LastMember_DeletesTeamFromPostgres()
    {
        var client = fixture.Factory.CreateClient();
        var team = await CreateTeamAsync(client, Guid.NewGuid()); // 1 miembro

        var response = await client.PostAsync($"/api/teams/{team.TeamId}/leave", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));

        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamsDbContext>();
        (await dbContext.Teams.AnyAsync(t => t.Id == team.TeamId)).Should().BeFalse();
    }

    // ── POST /api/teams/{id}/penalize ───────────────────────────────────────────

    [Fact]
    public async Task PenalizeTeam_AppliesPenaltyInPostgres()
    {
        var client = fixture.Factory.CreateClient();
        var team = await CreateTeamAsync(client, Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            $"/api/teams/{team.TeamId}/penalize", new PenalizeTeamRequest(Points: 5, Reason: "Uso de celular"));
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));

        var payload = await response.Content.ReadFromJsonAsync<NewScoreResponse>(CaseInsensitive);
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamsDbContext>();
        var persisted = await dbContext.Teams.SingleAsync(t => t.Id == team.TeamId);
        persisted.Score.Should().Be(payload!.NewScore);
    }

    [Fact]
    public async Task PenalizeTeam_UnknownId_ReturnsNotFound()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/teams/{Guid.NewGuid()}/penalize", new PenalizeTeamRequest(1, "x"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/teams/{id}/force-advance ──────────────────────────────────────

    [Fact]
    public async Task ForceAdvanceTeam_MovesToNextStageInPostgres()
    {
        var client = fixture.Factory.CreateClient();
        var team = await CreateTeamAsync(client, Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            $"/api/teams/{team.TeamId}/force-advance", new ForceAdvanceTeamRequest(NextStageOrder: 2));
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));

        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeamsDbContext>();
        var persisted = await dbContext.Teams.SingleAsync(t => t.Id == team.TeamId);
        persisted.CurrentStageOrder.Should().Be(2);
    }
}
