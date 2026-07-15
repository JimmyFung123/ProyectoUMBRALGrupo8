// See StageAddedFanOutFixture.cs for why this alias is needed: ClueService's CluesDbContext
// lives in an assembly that also declares its own top-level-statements `Program`, which
// would otherwise collide with MissionService's `Program` if referenced unqualified.
extern alias ClueServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Messaging;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMBRAL_Back_end.Infrastructure.Persistence;
using ClueServiceAssembly::ClueService.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// Level-2 pilot: publishes a single <see cref="UMBRAL.Contracts.Events.StageAddedIntegrationEvent"/>
/// to a real RabbitMQ broker and verifies it is independently delivered to BOTH
/// MissionService's and ClueService's own <c>StageAddedConsumer</c> — the fan-out scenario
/// that justified giving each service its own queue prefix ("mission-...", "clue-...") in
/// <c>AddUmbralMassTransit</c>. If this passes, both services really do get their own copy
/// of the message instead of racing each other for a single shared queue.
/// </summary>
[Collection(StageAddedFanOutCollection.Name)]
public class StageAddedFanOutTests(StageAddedFanOutFixture fixture)
{
    [Fact]
    public async Task PublishStageAdded_OverRealRabbitMq_IsConsumedIndependentlyByMissionAndClueService()
    {
        var stageId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        const string stageType = "Trivia";

        await fixture.PublishStageAddedAsync(stageId, missionId, stageType);

        // (a) MissionService's StageAddedConsumer must create/increment a StageCountLookup
        // row for this MissionId. It is the mission's first stage, so Count should land on 1.
        var missionLookupCount = await PollAsync(async () =>
        {
            using var scope = fixture.MissionFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var lookup = await db.StageCountLookup.AsNoTracking()
                .SingleOrDefaultAsync(l => l.MissionId == missionId);
            return lookup?.Count;
        }, count => count == 1);

        missionLookupCount.Should().Be(1,
            "MissionService's own StageAddedConsumer should have received the event over the real broker and projected a StageCountLookup row");

        // (b) ClueService's StageAddedConsumer must create a StageLookup row keyed by StageId,
        // completely independently of (a) — same event, same broker, different queue.
        var clueLookupExists = await PollAsync(async () =>
        {
            using var scope = fixture.ClueFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CluesDbContext>();
            return await db.StagesLookup.AsNoTracking().AnyAsync(l => l.Id == stageId);
        }, exists => exists);

        clueLookupExists.Should().BeTrue(
            "ClueService's own StageAddedConsumer should have received the SAME event over the real broker and projected a StageLookup row, proving the fan-out reaches both services");
    }

    /// <summary>
    /// Polls <paramref name="probe"/> every 300ms (up to 10s total) until
    /// <paramref name="isDone"/> is satisfied. Consumption over a real broker is
    /// asynchronous — an immediate assert right after publish is flaky by construction,
    /// so every read in this test class goes through this helper instead.
    /// </summary>
    private static async Task<T> PollAsync<T>(
        Func<Task<T>> probe,
        Func<T, bool> isDone,
        TimeSpan? timeout = null,
        TimeSpan? interval = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        var delay = interval ?? TimeSpan.FromMilliseconds(300);

        var last = await probe();
        while (!isDone(last) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(delay);
            last = await probe();
        }

        return last;
    }
}
