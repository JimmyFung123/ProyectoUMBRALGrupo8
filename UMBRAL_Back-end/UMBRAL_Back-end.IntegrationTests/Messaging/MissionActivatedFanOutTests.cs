// See MissionActivatedFanOutFixture.cs for why these aliases are needed.
extern alias SessionServiceAssembly;
extern alias StageServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Messaging;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using SessionServiceAssembly::SessionService.Infrastructure.Persistence;
using StageServiceAssembly::StageService.Domain.MissionLookup;
using StageServiceAssembly::StageService.Infrastructure.Persistence;
using Xunit;
using static UMBRAL_Back_end.IntegrationTests.Infrastructure.Polling;

/// <summary>
/// Level-2: publishes a single <see cref="UMBRAL.Contracts.Events.MissionActivatedIntegrationEvent"/>
/// to a real RabbitMQ broker and verifies the two consuming services react correctly —
/// asymmetrically, by design:
///
///   * SessionService's <c>MissionActivatedConsumer</c> self-heals: if the MissionCreated
///     event never arrived (e.g. this consumer was offline), it creates the MissionLookup
///     row from the data carried by THIS event instead of no-op'ing. Tested here with no
///     pre-seed, proving the self-heal path works over a real broker.
///   * StageService's <c>MissionActivatedConsumer</c> does NOT self-heal: it is a pure
///     status flip and no-ops if the row is missing. Tested here WITH a pre-seeded row,
///     since the no-op path can't be observed by polling for a row that never appears.
/// </summary>
[Collection(MissionActivatedFanOutCollection.Name)]
public class MissionActivatedFanOutTests(MissionActivatedFanOutFixture fixture)
{
    [Fact]
    public async Task PublishMissionActivated_OverRealRabbitMq_SelfHealsInSessionServiceAndUpdatesStageService()
    {
        var missionId = Guid.NewGuid();
        const string name = "Mission Activated Fan-Out";
        const string difficulty = "Hard";

        using (var stageScope = fixture.StageFactory.Services.CreateScope())
        {
            var db = stageScope.ServiceProvider.GetRequiredService<StagesDbContext>();
            db.MissionsLookup.Add(MissionLookup.Create(missionId, name, "Inactive"));
            await db.SaveChangesAsync();
        }

        await fixture.PublishMissionActivatedAsync(missionId, name, difficulty);

        var sessionLookup = await PollAsync(async () =>
        {
            using var scope = fixture.SessionFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
            return await db.MissionsLookup.AsNoTracking()
                .SingleOrDefaultAsync(l => l.Id == missionId);
        }, lookup => lookup is { Status: "Active" });

        sessionLookup.Should().NotBeNull(
            "SessionService's MissionActivatedConsumer should self-heal and create the MissionLookup row even without a prior MissionCreated event");
        sessionLookup!.Status.Should().Be("Active");
        sessionLookup.Difficulty.Should().Be(difficulty);

        var stageStatus = await PollAsync(async () =>
        {
            using var scope = fixture.StageFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StagesDbContext>();
            var lookup = await db.MissionsLookup.AsNoTracking()
                .SingleOrDefaultAsync(l => l.Id == missionId);
            return lookup?.Status;
        }, status => status == "Active");

        stageStatus.Should().Be("Active",
            "StageService's MissionActivatedConsumer does not self-heal, so the seeded row must have been flipped to Active");
    }
}
