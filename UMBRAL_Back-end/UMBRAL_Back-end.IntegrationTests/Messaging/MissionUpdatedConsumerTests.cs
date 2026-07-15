// See MissionUpdatedConsumerFixture.cs for why this alias is needed.
extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Messaging;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using SessionServiceAssembly::SessionService.Domain.MissionLookup;
using SessionServiceAssembly::SessionService.Infrastructure.Persistence;
using Xunit;
using static UMBRAL_Back_end.IntegrationTests.Infrastructure.Polling;

/// <summary>
/// Level-2: publishes a single <see cref="UMBRAL.Contracts.Events.MissionUpdatedIntegrationEvent"/>
/// to a real RabbitMQ broker and verifies SessionService's <c>MissionUpdatedConsumer</c>
/// updates the pre-seeded MissionLookup's Difficulty. The consumer no-ops if the row is
/// absent (mission not yet in the lookup — it will be added on the next Activate), so this
/// test always pre-seeds a row.
/// </summary>
[Collection(MissionUpdatedConsumerCollection.Name)]
public class MissionUpdatedConsumerTests(MissionUpdatedConsumerFixture fixture)
{
    [Fact]
    public async Task PublishMissionUpdated_OverRealRabbitMq_UpdatesSeededDifficulty()
    {
        var missionId = Guid.NewGuid();
        const string name = "Mission Updated Consumer";
        const string initialDifficulty = "Easy";
        const string newDifficulty = "Hard";

        using (var scope = fixture.SessionFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
            db.MissionsLookup.Add(MissionLookup.Create(missionId, name, "Active", initialDifficulty));
            await db.SaveChangesAsync();
        }

        await fixture.PublishMissionUpdatedAsync(missionId, name, newDifficulty);

        var difficulty = await PollAsync(async () =>
        {
            using var scope = fixture.SessionFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
            var lookup = await db.MissionsLookup.AsNoTracking().SingleOrDefaultAsync(l => l.Id == missionId);
            return lookup?.Difficulty;
        }, d => d == newDifficulty);

        difficulty.Should().Be(newDifficulty,
            "SessionService's MissionUpdatedConsumer should have received the event over the real broker and updated the seeded MissionLookup's Difficulty");
    }
}
