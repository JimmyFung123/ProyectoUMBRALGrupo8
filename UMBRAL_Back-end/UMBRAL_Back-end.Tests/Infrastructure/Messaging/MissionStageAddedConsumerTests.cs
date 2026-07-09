namespace UMBRAL_Back_end.Tests.Infrastructure.Messaging;

using FluentAssertions;
using MassTransit;
using Moq;
using UMBRAL.Contracts.Events;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Infrastructure.Messaging.Consumers;
using Xunit;

public class MissionStageAddedConsumerTests
{
    private readonly Mock<IStageCountLookupRepository> _repoMock = new();
    private readonly StageAddedConsumer _consumer;

    public MissionStageAddedConsumerTests()
        => _consumer = new StageAddedConsumer(_repoMock.Object);

    private static Mock<ConsumeContext<StageAddedIntegrationEvent>> ContextFor(Guid missionId)
    {
        var ctx = new Mock<ConsumeContext<StageAddedIntegrationEvent>>();
        ctx.SetupGet(c => c.Message)
           .Returns(new StageAddedIntegrationEvent(Guid.NewGuid(), missionId, "Trivia", DateTime.UtcNow));
        ctx.SetupGet(c => c.CancellationToken).Returns(default(CancellationToken));
        return ctx;
    }

    [Fact]
    public async Task Consume_WhenNoCounterYet_CreatesCounterAndSaves()
    {
        var missionId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByMissionIdAsync(missionId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((StageCountLookup?)null);

        await _consumer.Consume(ContextFor(missionId).Object);

        _repoMock.Verify(r => r.AddAsync(It.IsAny<StageCountLookup>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenCounterExists_IncrementsWithoutAdding()
    {
        var missionId = Guid.NewGuid();
        var counter = StageCountLookup.Create(missionId);
        _repoMock.Setup(r => r.GetByMissionIdAsync(missionId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(counter);

        await _consumer.Consume(ContextFor(missionId).Object);

        // Create() seeds Count = 1, so after one Increment it is 2.
        counter.Count.Should().Be(2);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<StageCountLookup>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
