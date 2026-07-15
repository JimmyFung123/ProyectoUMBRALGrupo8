// See StageRemovedFanOutFixture.cs for why this alias is needed.
extern alias ClueServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Messaging;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Infrastructure.Persistence;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using ClueServiceAssembly::ClueService.Domain.StageLookup;
using ClueServiceAssembly::ClueService.Infrastructure.Persistence;
using Xunit;
using static UMBRAL_Back_end.IntegrationTests.Infrastructure.Polling;

/// <summary>
/// Level-2: publishes a single <see cref="UMBRAL.Contracts.Events.StageRemovedIntegrationEvent"/>
/// to a real RabbitMQ broker and verifies it is independently delivered to BOTH
/// MissionService's and ClueService's own <c>StageRemovedConsumer</c> — the removal-path
/// mirror of <see cref="StageAddedFanOutTests"/>.
/// </summary>
[Collection(StageRemovedFanOutCollection.Name)]
public class StageRemovedFanOutTests(StageRemovedFanOutFixture fixture)
{
    [Fact]
    public async Task PublishStageRemoved_OverRealRabbitMq_IsConsumedIndependentlyByMissionAndClueService()
    {
        var stageId = Guid.NewGuid();
        var missionId = Guid.NewGuid();

        // Both consumers are pure state-updaters (no self-heal / create-on-absent path),
        // so each side needs a pre-existing row for the event to have an observable effect.
        using (var missionScope = fixture.MissionFactory.Services.CreateScope())
        {
            var db = missionScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.StageCountLookup.Add(StageCountLookup.Create(missionId));
            await db.SaveChangesAsync();
        }

        using (var clueScope = fixture.ClueFactory.Services.CreateScope())
        {
            var db = clueScope.ServiceProvider.GetRequiredService<CluesDbContext>();
            db.StagesLookup.Add(StageLookup.Create(stageId, missionId, "Seeded stage"));
            await db.SaveChangesAsync();
        }

        await fixture.PublishStageRemovedAsync(stageId, missionId);

        // (a) MissionService's StageRemovedConsumer decrements StageCountLookup.Count
        // (floored at 0) for this MissionId — it started at 1, so it should reach 0.
        var missionLookupCount = await PollAsync(async () =>
        {
            using var scope = fixture.MissionFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var lookup = await db.StageCountLookup.AsNoTracking()
                .SingleOrDefaultAsync(l => l.MissionId == missionId);
            return lookup?.Count;
        }, count => count == 0);

        missionLookupCount.Should().Be(0,
            "MissionService's own StageRemovedConsumer should have decremented the seeded StageCountLookup row over the real broker");

        // (b) ClueService's StageRemovedConsumer deletes the StageLookup row keyed by StageId.
        var clueLookupExists = await PollAsync(async () =>
        {
            using var scope = fixture.ClueFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CluesDbContext>();
            return await db.StagesLookup.AsNoTracking().AnyAsync(l => l.Id == stageId);
        }, exists => !exists);

        clueLookupExists.Should().BeFalse(
            "ClueService's own StageRemovedConsumer should have deleted the seeded StageLookup row over the real broker");
    }
}
