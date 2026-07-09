namespace UMBRAL_Back_end.Tests.Infrastructure.Messaging;

using FluentAssertions;
using MassTransit;
using Moq;
using UMBRAL.Contracts.Events;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Infrastructure.Messaging.Consumers;
using Xunit;

public class MissionStageRemovedConsumerTests
{
    private readonly Mock<IStageCountLookupRepository> _repoMock = new();
    private readonly StageRemovedConsumer _consumer;

    public MissionStageRemovedConsumerTests()
        => _consumer = new StageRemovedConsumer(_repoMock.Object);

    private static Mock<ConsumeContext<StageRemovedIntegrationEvent>> ContextFor(Guid missionId)
    {
        var ctx = new Mock<ConsumeContext<StageRemovedIntegrationEvent>>();
        ctx.SetupGet(c => c.Message)
           .Returns(new StageRemovedIntegrationEvent(Guid.NewGuid(), missionId, DateTime.UtcNow));
        ctx.SetupGet(c => c.CancellationToken).Returns(default(CancellationToken));
        return ctx;
    }

    [Fact]
    public async Task Consume_WhenCounterExists_DecrementsAndSaves()
    {
        var missionId = Guid.NewGuid();
        var counter = StageCountLookup.Create(missionId); // Count = 1
        counter.Increment();                               // Count = 2
        _repoMock.Setup(r => r.GetByMissionIdAsync(missionId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(counter);

        await _consumer.Consume(ContextFor(missionId).Object);

        counter.Count.Should().Be(1);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenNoCounter_DoesNothing()
    {
        var missionId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByMissionIdAsync(missionId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((StageCountLookup?)null);

        await _consumer.Consume(ContextFor(missionId).Object);

        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
