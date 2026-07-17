namespace UMBRAL_Back_end.Tests.Infrastructure.Messaging;

using MassTransit;
using Moq;
using SessionService.Application.Sessions;
using SessionService.Infrastructure.Messaging.Consumers;
using UMBRAL.Contracts.Events;
using Xunit;

public class StageCompletedConsumerTests
{
    private readonly Mock<ISessionNotifier> _notifierMock = new();
    private readonly StageCompletedConsumer _consumer;

    public StageCompletedConsumerTests()
        => _consumer = new StageCompletedConsumer(_notifierMock.Object);

    [Fact]
    public async Task Consume_RelaysStageCompletedToNotifier()
    {
        using var cts = new CancellationTokenSource();
        var sessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var evt = new StageCompletedIntegrationEvent(
            sessionId, teamId, StageOrder: 2, StageType: "Trivia",
            WasCorrect: true, PointsEarned: 50, NewScore: 150, NextStageOrder: 3, IsLastStage: false);

        var ctx = new Mock<ConsumeContext<StageCompletedIntegrationEvent>>();
        ctx.SetupGet(c => c.Message).Returns(evt);
        ctx.SetupGet(c => c.CancellationToken).Returns(cts.Token);

        await _consumer.Consume(ctx.Object);

        _notifierMock.Verify(n => n.NotifyStageCompletedAsync(
            sessionId, teamId, 2, "Trivia", true, 50, 150, 3, false, cts.Token), Times.Once);
    }
}
