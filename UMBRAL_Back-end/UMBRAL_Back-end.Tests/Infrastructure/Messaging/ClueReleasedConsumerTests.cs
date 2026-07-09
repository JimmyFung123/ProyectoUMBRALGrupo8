namespace UMBRAL_Back_end.Tests.Infrastructure.Messaging;

using MassTransit;
using Moq;
using SessionService.Application.Sessions;
using SessionService.Infrastructure.Messaging.Consumers;
using UMBRAL.Contracts.Events;
using Xunit;

public class ClueReleasedConsumerTests
{
    private readonly Mock<ISessionNotifier> _notifierMock = new();
    private readonly ClueReleasedConsumer _consumer;

    public ClueReleasedConsumerTests()
        => _consumer = new ClueReleasedConsumer(_notifierMock.Object);

    [Fact]
    public async Task Consume_RelaysClueReleasedToNotifier()
    {
        using var cts = new CancellationTokenSource();
        var sessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var evt = new ClueReleasedIntegrationEvent(
            sessionId, teamId, "Busca el árbol", 10.5, -66.9, 50, 3, IsAutomatic: true);

        var ctx = new Mock<ConsumeContext<ClueReleasedIntegrationEvent>>();
        ctx.SetupGet(c => c.Message).Returns(evt);
        ctx.SetupGet(c => c.CancellationToken).Returns(cts.Token);

        await _consumer.Consume(ctx.Object);

        _notifierMock.Verify(n => n.NotifyClueReleasedAsync(
            sessionId, teamId, "Busca el árbol", 10.5, -66.9, 50, 3, true, cts.Token), Times.Once);
    }
}
