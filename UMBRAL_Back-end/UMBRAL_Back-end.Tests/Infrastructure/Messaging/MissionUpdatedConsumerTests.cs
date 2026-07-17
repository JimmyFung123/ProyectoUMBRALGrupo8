namespace UMBRAL_Back_end.Tests.Infrastructure.Messaging;

using MassTransit;
using Moq;
using SessionService.Domain.MissionLookup;
using SessionService.Infrastructure.Messaging.Consumers;
using UMBRAL.Contracts.Events;
using Xunit;

public class MissionUpdatedConsumerTests
{
    private readonly Mock<IMissionLookupRepository> _repoMock = new();
    private readonly MissionUpdatedConsumer _consumer;

    public MissionUpdatedConsumerTests()
        => _consumer = new MissionUpdatedConsumer(_repoMock.Object);

    private static Mock<ConsumeContext<MissionUpdatedIntegrationEvent>> ContextFor(Guid missionId)
    {
        var ctx = new Mock<ConsumeContext<MissionUpdatedIntegrationEvent>>();
        ctx.SetupGet(c => c.Message)
           .Returns(new MissionUpdatedIntegrationEvent(missionId, "Misión", "Hard", DateTime.UtcNow));
        ctx.SetupGet(c => c.CancellationToken).Returns(default(CancellationToken));
        return ctx;
    }

    [Fact]
    public async Task Consume_WhenLookupExists_UpdatesDifficultyAndSaves()
    {
        var missionId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(MissionLookup.Create(missionId, "Misión", "Active"));

        await _consumer.Consume(ContextFor(missionId).Object);

        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenLookupMissing_DoesNothing()
    {
        var missionId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((MissionLookup?)null);

        await _consumer.Consume(ContextFor(missionId).Object);

        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
