// See MissionCreatedFanOutFixture.cs for why these aliases are needed.
extern alias SessionServiceAssembly;
extern alias StageServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Messaging;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using SessionServiceAssembly::SessionService.Infrastructure.Persistence;
using StageServiceAssembly::StageService.Infrastructure.Persistence;
using Xunit;
using static UMBRAL_Back_end.IntegrationTests.Infrastructure.Polling;

/// <summary>
/// Level-2: publishes a single <see cref="UMBRAL.Contracts.Events.MissionCreatedIntegrationEvent"/>
/// to a real RabbitMQ broker and verifies it is independently delivered to BOTH
/// SessionService's and StageService's own <c>MissionCreatedConsumer</c>. Unlike the
/// Activated/Deactivated pair, both consumers here behave identically: idempotent
/// create-if-absent, no pre-seed required.
/// </summary>
[Collection(MissionCreatedFanOutCollection.Name)]
public class MissionCreatedFanOutTests(MissionCreatedFanOutFixture fixture)
{
    [Fact]
    public async Task PublishMissionCreated_OverRealRabbitMq_SeedsLookupInBothSessionAndStageService()
    {
        var missionId = Guid.NewGuid();
        const string name = "Mission Created Fan-Out";
        const string status = "Inactive";

        await fixture.PublishMissionCreatedAsync(missionId, name, status);

        var sessionLookup = await PollAsync(async () =>
        {
            using var scope = fixture.SessionFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
            return await db.MissionsLookup.AsNoTracking().SingleOrDefaultAsync(l => l.Id == missionId);
        }, lookup => lookup is not null);

        sessionLookup.Should().NotBeNull(
            "SessionService's own MissionCreatedConsumer should have received the event over the real broker");
        sessionLookup!.Name.Should().Be(name);
        sessionLookup.Status.Should().Be(status);

        var stageLookup = await PollAsync(async () =>
        {
            using var scope = fixture.StageFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StagesDbContext>();
            return await db.MissionsLookup.AsNoTracking().SingleOrDefaultAsync(l => l.Id == missionId);
        }, lookup => lookup is not null);

        stageLookup.Should().NotBeNull(
            "StageService's own MissionCreatedConsumer should have received the SAME event over the real broker, proving the fan-out reaches both services");
        stageLookup!.Name.Should().Be(name);
        stageLookup.Status.Should().Be(status);
    }
}
