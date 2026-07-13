namespace UMBRAL_Back_end.Tests.Infrastructure.Messaging;

using MassTransit;
using Moq;
using StageService.Domain.MissionLookup;
using StageService.Infrastructure.Messaging.Consumers;
using UMBRAL.Contracts.Events;
using Xunit;

public class StageMissionCreatedConsumerTests
{
    private readonly Mock<IMissionLookupRepository> _repoMock = new();
    private readonly MissionCreatedConsumer _consumer;

    public StageMissionCreatedConsumerTests()
        => _consumer = new MissionCreatedConsumer(_repoMock.Object);

    private static Mock<ConsumeContext<MissionCreatedIntegrationEvent>> ContextFor(Guid missionId)
    {
        var ctx = new Mock<ConsumeContext<MissionCreatedIntegrationEvent>>();
        ctx.SetupGet(c => c.Message)
           .Returns(new MissionCreatedIntegrationEvent(missionId, "Misión", "Inactive", DateTime.UtcNow));
        ctx.SetupGet(c => c.CancellationToken).Returns(default(CancellationToken));
        return ctx;
    }

    [Fact]
    public async Task Consume_WhenNotSeeded_AddsLookupAndSaves()
    {
        var missionId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((MissionLookup?)null);

        await _consumer.Consume(ContextFor(missionId).Object);

        _repoMock.Verify(r => r.AddAsync(It.IsAny<MissionLookup>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenAlreadySeeded_IsIdempotent()
    {
        var missionId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(MissionLookup.Create(missionId, "Misión", "Inactive"));

        await _consumer.Consume(ContextFor(missionId).Object);

        _repoMock.Verify(r => r.AddAsync(It.IsAny<MissionLookup>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
