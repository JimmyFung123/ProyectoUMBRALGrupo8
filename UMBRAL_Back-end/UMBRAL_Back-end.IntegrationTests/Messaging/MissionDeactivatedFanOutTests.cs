// See MissionDeactivatedFanOutFixture.cs for why these aliases are needed.
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
/// Level-2: publishes a single <see cref="UMBRAL.Contracts.Events.MissionDeactivatedIntegrationEvent"/>
/// to a real RabbitMQ broker and verifies the two consuming services react correctly —
/// asymmetrically, mirroring <see cref="MissionActivatedFanOutTests"/>:
///
///   * SessionService's <c>MissionActivatedConsumer</c>-equivalent for Deactivated
///     self-heals: creates the MissionLookup row with Status="Inactive" if absent.
///     Tested here with no pre-seed.
///   * StageService's consumer does NOT self-heal: no-op if the row is missing, so the
///     test pre-seeds a row with Status="Active" and polls for the flip to "Inactive".
/// </summary>
[Collection(MissionDeactivatedFanOutCollection.Name)]
public class MissionDeactivatedFanOutTests(MissionDeactivatedFanOutFixture fixture)
{
    [Fact]
    public async Task PublishMissionDeactivated_OverRealRabbitMq_SelfHealsInSessionServiceAndUpdatesStageService()
    {
        var missionId = Guid.NewGuid();
        const string name = "Mission Deactivated Fan-Out";

        using (var stageScope = fixture.StageFactory.Services.CreateScope())
        {
            var db = stageScope.ServiceProvider.GetRequiredService<StagesDbContext>();
            db.MissionsLookup.Add(MissionLookup.Create(missionId, name, "Active"));
            await db.SaveChangesAsync();
        }

        await fixture.PublishMissionDeactivatedAsync(missionId, name);

        var sessionLookup = await PollAsync(async () =>
        {
            using var scope = fixture.SessionFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
            return await db.MissionsLookup.AsNoTracking().SingleOrDefaultAsync(l => l.Id == missionId);
        }, lookup => lookup is { Status: "Inactive" });

        sessionLookup.Should().NotBeNull(
            "SessionService's MissionDeactivatedConsumer should self-heal and create the MissionLookup row even without a prior MissionCreated event");
        sessionLookup!.Status.Should().Be("Inactive");

        var stageStatus = await PollAsync(async () =>
        {
            using var scope = fixture.StageFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StagesDbContext>();
            var lookup = await db.MissionsLookup.AsNoTracking().SingleOrDefaultAsync(l => l.Id == missionId);
            return lookup?.Status;
        }, status => status == "Inactive");

        stageStatus.Should().Be("Inactive",
            "StageService's MissionDeactivatedConsumer does not self-heal, so the seeded row must have been flipped to Inactive");
    }
}
