namespace UMBRAL_Back_end.Tests.Infrastructure.Messaging;

using MassTransit;
using Moq;
using SessionService.Application.Sessions;
using SessionService.Infrastructure.Messaging.Consumers;
using UMBRAL.Contracts.Events;
using Xunit;

public class OperatorMessageBroadcastConsumerTests
{
    private readonly Mock<ISessionNotifier> _notifierMock = new();
    private readonly OperatorMessageBroadcastConsumer _consumer;

    public OperatorMessageBroadcastConsumerTests()
        => _consumer = new OperatorMessageBroadcastConsumer(_notifierMock.Object);

    [Fact]
    public async Task Consume_RelaysOperatorMessageToNotifier()
    {
        using var cts = new CancellationTokenSource();
        var sessionId = Guid.NewGuid();
        var deliveredAt = DateTime.UtcNow;
        var evt = new OperatorMessageBroadcastIntegrationEvent(
            sessionId, "¡Última etapa!", "Profesor", deliveredAt);

        var ctx = new Mock<ConsumeContext<OperatorMessageBroadcastIntegrationEvent>>();
        ctx.SetupGet(c => c.Message).Returns(evt);
        ctx.SetupGet(c => c.CancellationToken).Returns(cts.Token);

        await _consumer.Consume(ctx.Object);

        _notifierMock.Verify(n => n.NotifyOperatorMessageAsync(
            sessionId, "¡Última etapa!", "Profesor", deliveredAt, cts.Token), Times.Once);
    }
}
