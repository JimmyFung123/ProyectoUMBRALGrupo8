// This file touches types that live in TeamService's aliased assembly (see the
// .csproj comment on the TeamService ProjectReference for why the alias exists).
extern alias TeamServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.SyncHealth;

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using TeamServiceAssembly::TeamService.Adapter.Controllers;
using TeamServiceAssembly::TeamService.Domain.Teams;
using TeamServiceAssembly::TeamService.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// Unlike the other three services' reproject, this one makes no outbound HTTP call —
/// it delegates straight to the real <c>IRankingProjector</c>, which rebuilds
/// RankingProjections from the Teams write model already in Postgres. No stub needed.
/// </summary>
[Collection(TeamServiceCollection.Name)]
public class TeamServiceReprojectRankingTests(TeamServicePostgresFixture fixture) : IAsyncLifetime
{
    // xUnit creates a new instance of this class per [Fact], so InitializeAsync runs before
    // EACH test — unlike the collection fixture, which is shared by the whole class. Resetting
    // here keeps tests isolated from rows left over by earlier tests in the same collection.
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task ReprojectSessionRanking_RebuildsProjection_FromSeededTeam()
    {
        var sessionId = Guid.NewGuid();
        var teamNameResult = TeamName.Create("Los Exploradores");
        teamNameResult.IsSuccess.Should().BeTrue();
        var team = Team.Create(sessionId, teamNameResult.Value);

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TeamsDbContext>();
            dbContext.Teams.Add(team);
            await dbContext.SaveChangesAsync();
        }

        var client = fixture.Factory.CreateClient();
        var response = await client.PostAsync($"/api/internal/sync-health/ranking/{sessionId}/reproject", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        var result = JsonSerializer.Deserialize<RankingReprojectResultDto>(
            await response.Content.ReadAsStringAsync(), JsonOptions);
        result.Should().NotBeNull();
        result!.SessionId.Should().Be(sessionId);
        result.TeamCount.Should().Be(1);
        result.ProjectionCount.Should().Be(1);

        using var assertScope = fixture.Factory.Services.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<TeamsDbContext>();
        var projectionExists = await assertDbContext.RankingProjections
            .AnyAsync(p => p.SessionId == sessionId && p.TeamId == team.Id);
        projectionExists.Should().BeTrue();
    }
}
