namespace UMBRAL_Back_end.Tests.Infrastructure.Messaging;

using MassTransit;
using Moq;
using SessionService.Application.Sessions;
using SessionService.Infrastructure.Messaging.Consumers;
using UMBRAL.Contracts.Events;
using Xunit;

public class TeamPenalizedConsumerTests
{
    private readonly Mock<ISessionNotifier> _notifierMock = new();
    private readonly TeamPenalizedConsumer _consumer;

    public TeamPenalizedConsumerTests()
        => _consumer = new TeamPenalizedConsumer(_notifierMock.Object);

    [Fact]
    public async Task Consume_RelaysTeamPenalizedToNotifier()
    {
        using var cts = new CancellationTokenSource();
        var sessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var evt = new TeamPenalizedIntegrationEvent(
            sessionId, teamId, "Los Halcones", Points: 10, Reason: "Falta", NewScore: 90, ActorName: "Profesor");

        var ctx = new Mock<ConsumeContext<TeamPenalizedIntegrationEvent>>();
        ctx.SetupGet(c => c.Message).Returns(evt);
        ctx.SetupGet(c => c.CancellationToken).Returns(cts.Token);

        await _consumer.Consume(ctx.Object);

        _notifierMock.Verify(n => n.NotifyTeamPenalizedAsync(
            sessionId, teamId, "Los Halcones", 10, "Falta", 90, "Profesor", cts.Token), Times.Once);
    }
}
