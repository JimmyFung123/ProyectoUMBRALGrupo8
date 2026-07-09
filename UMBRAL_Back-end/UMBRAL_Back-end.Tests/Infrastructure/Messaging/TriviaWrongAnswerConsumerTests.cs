namespace UMBRAL_Back_end.Tests.Infrastructure.Messaging;

using MassTransit;
using Moq;
using SessionService.Application.Sessions;
using SessionService.Infrastructure.Messaging.Consumers;
using UMBRAL.Contracts.Events;
using Xunit;

public class TriviaWrongAnswerConsumerTests
{
    private readonly Mock<ISessionNotifier> _notifierMock = new();
    private readonly TriviaWrongAnswerConsumer _consumer;

    public TriviaWrongAnswerConsumerTests()
        => _consumer = new TriviaWrongAnswerConsumer(_notifierMock.Object);

    [Fact]
    public async Task Consume_RelaysTriviaWrongAnswerToNotifier()
    {
        using var cts = new CancellationTokenSource();
        var sessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var blockedOptionId = Guid.NewGuid();
        var evt = new TriviaWrongAnswerIntegrationEvent(
            sessionId, teamId, StageOrder: 1, BlockedOptionId: blockedOptionId,
            AttemptsUsed: 1, MaxAttempts: 3, ScoreChange: -25, NewScore: 75, ParticipantName: "Ana");

        var ctx = new Mock<ConsumeContext<TriviaWrongAnswerIntegrationEvent>>();
        ctx.SetupGet(c => c.Message).Returns(evt);
        ctx.SetupGet(c => c.CancellationToken).Returns(cts.Token);

        await _consumer.Consume(ctx.Object);

        _notifierMock.Verify(n => n.NotifyTriviaWrongAnswerAsync(
            sessionId, teamId, 1, blockedOptionId, 1, 3, -25, 75, "Ana", cts.Token), Times.Once);
    }
}
