namespace UMBRAL_Back_end.Tests.Infrastructure.Messaging;

using MassTransit;
using Moq;
using SessionService.Domain.MissionLookup;
using SessionService.Infrastructure.Messaging.Consumers;
using UMBRAL.Contracts.Events;
using Xunit;

public class MissionDeactivatedConsumerTests
{
    private readonly Mock<IMissionLookupRepository> _repoMock = new();
    private readonly MissionDeactivatedConsumer _consumer;

    public MissionDeactivatedConsumerTests()
        => _consumer = new MissionDeactivatedConsumer(_repoMock.Object);

    private static Mock<ConsumeContext<MissionDeactivatedIntegrationEvent>> ContextFor(Guid missionId)
    {
        var ctx = new Mock<ConsumeContext<MissionDeactivatedIntegrationEvent>>();
        ctx.SetupGet(c => c.Message)
           .Returns(new MissionDeactivatedIntegrationEvent(missionId, "Misión", DateTime.UtcNow));
        ctx.SetupGet(c => c.CancellationToken).Returns(default(CancellationToken));
        return ctx;
    }

    [Fact]
    public async Task Consume_WhenLookupMissing_SelfHealsByAddingInactiveLookup()
    {
        var missionId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((MissionLookup?)null);

        await _consumer.Consume(ContextFor(missionId).Object);

        _repoMock.Verify(r => r.AddAsync(It.IsAny<MissionLookup>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenLookupExists_UpdatesStatusWithoutAdding()
    {
        var missionId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(MissionLookup.Create(missionId, "Misión", "Active"));

        await _consumer.Consume(ContextFor(missionId).Object);

        _repoMock.Verify(r => r.AddAsync(It.IsAny<MissionLookup>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
