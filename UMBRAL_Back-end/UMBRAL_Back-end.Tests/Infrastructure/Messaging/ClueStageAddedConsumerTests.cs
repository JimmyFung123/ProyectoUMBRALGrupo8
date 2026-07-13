namespace UMBRAL_Back_end.Tests.Infrastructure.Messaging;

using ClueService.Domain.StageLookup;
using ClueService.Infrastructure.Messaging.Consumers;
using MassTransit;
using Moq;
using UMBRAL.Contracts.Events;
using Xunit;

public class ClueStageAddedConsumerTests
{
    private readonly Mock<IStageLookupRepository> _repoMock = new();
    private readonly StageAddedConsumer _consumer;

    public ClueStageAddedConsumerTests()
        => _consumer = new StageAddedConsumer(_repoMock.Object);

    private static Mock<ConsumeContext<StageAddedIntegrationEvent>> ContextFor(Guid stageId, Guid missionId)
    {
        var ctx = new Mock<ConsumeContext<StageAddedIntegrationEvent>>();
        ctx.SetupGet(c => c.Message)
           .Returns(new StageAddedIntegrationEvent(stageId, missionId, "Trivia", DateTime.UtcNow));
        ctx.SetupGet(c => c.CancellationToken).Returns(default(CancellationToken));
        return ctx;
    }

    [Fact]
    public async Task Consume_WhenStageNotSeeded_AddsLookupAndSaves()
    {
        var stageId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((StageLookup?)null);

        await _consumer.Consume(ContextFor(stageId, Guid.NewGuid()).Object);

        _repoMock.Verify(r => r.AddAsync(It.IsAny<StageLookup>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenStageAlreadySeeded_IsIdempotent()
    {
        var stageId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(StageLookup.Create(stageId, missionId, "Trivia"));

        await _consumer.Consume(ContextFor(stageId, missionId).Object);

        _repoMock.Verify(r => r.AddAsync(It.IsAny<StageLookup>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
