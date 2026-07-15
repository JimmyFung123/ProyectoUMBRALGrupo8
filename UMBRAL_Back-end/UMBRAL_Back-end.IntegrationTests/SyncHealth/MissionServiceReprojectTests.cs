namespace UMBRAL_Back_end.IntegrationTests.SyncHealth;

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMBRAL_Back_end.Adapter.Controllers;
using UMBRAL_Back_end.Infrastructure.Persistence;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using Xunit;

/// <summary>
/// InternalSyncHealthTests only covers the GET (a local Postgres read); the POST
/// reproject action calls StageService over HTTP, which is out of scope for a plain
/// TestServer. UpstreamJsonStub gives that call a real address to hit so the full
/// rebuild path (group-by-mission, insert, drop-stale) runs against real Postgres.
/// </summary>
[Collection(MissionServiceCollection.Name)]
public class MissionServiceReprojectTests(MissionServicePostgresFixture fixture) : IAsyncLifetime
{
    // xUnit creates a new instance of this class per [Fact], so InitializeAsync runs before
    // EACH test — unlike the collection fixture, which is shared by the whole class. Resetting
    // here keeps tests isolated from rows left over by earlier tests in the same collection.
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Reproject_RebuildsStageCountLookup_FromUpstreamStagesFeed()
    {
        var missionId = Guid.NewGuid();
        var upstreamStages = new[]
        {
            new UpstreamStageDto(Guid.NewGuid(), missionId, "Stage 1"),
            new UpstreamStageDto(Guid.NewGuid(), missionId, "Stage 2"),
            new UpstreamStageDto(Guid.NewGuid(), missionId, "Stage 3"),
        };

        await using var stub = new UpstreamJsonStub();
        await stub.StartAsync(JsonSerializer.Serialize(upstreamStages));

        using var client = fixture.Factory
            .WithWebHostBuilder(b => b.UseSetting("StageServiceUrl", stub.BaseUrl))
            .CreateClient();

        var response = await client.PostAsync("/api/internal/sync-health/reproject", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        var result = JsonSerializer.Deserialize<MissionReprojectResultDto>(
            await response.Content.ReadAsStringAsync(), JsonOptions);
        result.Should().NotBeNull();
        result!.UpstreamStages.Should().Be(upstreamStages.Length);
        result.MissionsWithStages.Should().Be(1);

        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lookup = await dbContext.StageCountLookup.SingleAsync(s => s.MissionId == missionId);
        lookup.Count.Should().Be(upstreamStages.Length);
    }
}
