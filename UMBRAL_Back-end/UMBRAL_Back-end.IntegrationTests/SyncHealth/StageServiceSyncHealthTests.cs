// This file touches types that live in StageService's aliased assembly (see the
// .csproj comment on the StageService ProjectReference for why the alias exists).
extern alias StageServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.SyncHealth;

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using StageServiceAssembly::StageService.Adapter.Controllers;
using StageServiceAssembly::StageService.Domain.Stages;
using StageServiceAssembly::StageService.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// StagesFeed is a pure local read (no upstream call) — needs only seeded Stage rows.
/// Reproject calls MissionService over HTTP, out of scope for a plain TestServer, so
/// UpstreamJsonStub gives it a real address to hit and the MissionsLookup rebuild path
/// (upsert-by-id, drop-stale) runs against real Postgres.
/// </summary>
[Collection(StageServiceCollection.Name)]
public class StageServiceSyncHealthTests(StageServicePostgresFixture fixture) : IAsyncLifetime
{
    // xUnit creates a new instance of this class per [Fact], so InitializeAsync runs before
    // EACH test — unlike the collection fixture, which is shared by the whole class. Resetting
    // here keeps tests isolated from rows left over by earlier tests in the same collection.
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task StagesFeed_ReturnsOk_WithSeededStages()
    {
        var missionId = Guid.NewGuid();

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<StagesDbContext>();

            var first = Stage.Create(missionId, "Stage 1", StageType.Trivia, 1, 10, question: "¿Uno?");
            var second = Stage.Create(missionId, "Stage 2", StageType.Trivia, 2, 20, question: "¿Dos?");
            first.IsSuccess.Should().BeTrue();
            second.IsSuccess.Should().BeTrue();

            dbContext.Stages.Add(first.Value);
            dbContext.Stages.Add(second.Value);
            await dbContext.SaveChangesAsync();
        }

        var response = await fixture.Factory.CreateClient().GetAsync("/api/internal/sync-health/stages-feed");
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        var feed = JsonSerializer.Deserialize<List<StageFeedItemDto>>(
            await response.Content.ReadAsStringAsync(), JsonOptions);
        feed.Should().NotBeNull();
        feed!.Should().HaveCount(2);
        feed.Should().OnlyContain(s => s.MissionId == missionId);
    }

    [Fact]
    public async Task Reproject_RebuildsMissionsLookupFromUpstream()
    {
        var upstreamMissions = new[]
        {
            new UpstreamMissionDto(Guid.NewGuid(), "Misión 1", "Active"),
            new UpstreamMissionDto(Guid.NewGuid(), "Misión 2", "Draft"),
        };

        await using var stub = new UpstreamJsonStub();
        await stub.StartAsync(JsonSerializer.Serialize(upstreamMissions));

        using var client = fixture.Factory
            .WithWebHostBuilder(b => b.UseSetting("MissionServiceUrl", stub.BaseUrl))
            .CreateClient();

        var response = await client.PostAsync("/api/internal/sync-health/reproject", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        var result = JsonSerializer.Deserialize<ReprojectResultDto>(
            await response.Content.ReadAsStringAsync(), JsonOptions);
        result.Should().NotBeNull();
        result!.UpstreamCount.Should().Be(upstreamMissions.Length);

        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StagesDbContext>();
        foreach (var mission in upstreamMissions)
        {
            var row = await dbContext.MissionsLookup.SingleAsync(m => m.Id == mission.Id);
            row.Name.Should().Be(mission.Name);
            row.Status.Should().Be(mission.Status);
        }
    }
}
