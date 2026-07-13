namespace UMBRAL_Back_end.Tests.Infrastructure.Messaging;

using ClueService.Domain.StageLookup;
using ClueService.Infrastructure.Messaging.Consumers;
using MassTransit;
using Moq;
using UMBRAL.Contracts.Events;
using Xunit;

public class ClueStageRemovedConsumerTests
{
    private readonly Mock<IStageLookupRepository> _repoMock = new();
    private readonly StageRemovedConsumer _consumer;

    public ClueStageRemovedConsumerTests()
        => _consumer = new StageRemovedConsumer(_repoMock.Object);

    private static Mock<ConsumeContext<StageRemovedIntegrationEvent>> ContextFor(Guid stageId, Guid missionId)
    {
        var ctx = new Mock<ConsumeContext<StageRemovedIntegrationEvent>>();
        ctx.SetupGet(c => c.Message)
           .Returns(new StageRemovedIntegrationEvent(stageId, missionId, DateTime.UtcNow));
        ctx.SetupGet(c => c.CancellationToken).Returns(default(CancellationToken));
        return ctx;
    }

    [Fact]
    public async Task Consume_WhenStageExists_DeletesLookupAndSaves()
    {
        var stageId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var lookup = StageLookup.Create(stageId, missionId, "Trivia");
        _repoMock.Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(lookup);

        await _consumer.Consume(ContextFor(stageId, missionId).Object);

        _repoMock.Verify(r => r.DeleteAsync(lookup, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenStageMissing_DoesNothing()
    {
        var stageId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((StageLookup?)null);

        await _consumer.Consume(ContextFor(stageId, Guid.NewGuid()).Object);

        _repoMock.Verify(r => r.DeleteAsync(It.IsAny<StageLookup>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
