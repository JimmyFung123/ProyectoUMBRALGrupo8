namespace UMBRAL_Back_end.Tests.Infrastructure.Messaging;

using MassTransit;
using Moq;
using SessionService.Application.Sessions;
using SessionService.Infrastructure.Messaging.Consumers;
using UMBRAL.Contracts.Events;
using Xunit;

public class SessionStateChangedConsumerTests
{
    private readonly Mock<ISessionNotifier> _notifierMock = new();
    private readonly SessionStateChangedConsumer _consumer;

    public SessionStateChangedConsumerTests()
        => _consumer = new SessionStateChangedConsumer(_notifierMock.Object);

    [Fact]
    public async Task Consume_RelaysStateChangedToNotifier()
    {
        using var cts = new CancellationTokenSource();
        var sessionId = Guid.NewGuid();
        var evt = new SessionStateChangedIntegrationEvent(sessionId, "InProgress");

        var ctx = new Mock<ConsumeContext<SessionStateChangedIntegrationEvent>>();
        ctx.SetupGet(c => c.Message).Returns(evt);
        ctx.SetupGet(c => c.CancellationToken).Returns(cts.Token);

        await _consumer.Consume(ctx.Object);

        _notifierMock.Verify(n => n.NotifyStateChangedAsync(sessionId, "InProgress", cts.Token), Times.Once);
    }
}
