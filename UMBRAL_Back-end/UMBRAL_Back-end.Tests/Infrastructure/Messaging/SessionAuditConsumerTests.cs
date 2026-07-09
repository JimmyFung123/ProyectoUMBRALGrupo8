namespace UMBRAL_Back_end.Tests.Infrastructure.Messaging;

using FluentAssertions;
using MassTransit;
using Moq;
using SessionService.Domain.Sessions;
using SessionService.Infrastructure.Messaging.Consumers;
using UMBRAL.Contracts.Events;
using Xunit;

public class SessionAuditConsumerTests
{
    private readonly Mock<ISessionEventRepository> _repoMock = new();
    private readonly SessionAuditConsumer _consumer;

    public SessionAuditConsumerTests()
        => _consumer = new SessionAuditConsumer(_repoMock.Object);

    [Fact]
    public async Task Consume_PersistsSessionEventFromTheAuditMessage()
    {
        using var cts = new CancellationTokenSource();
        var sessionId = Guid.NewGuid();
        SessionEvent? captured = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<SessionEvent>(), It.IsAny<CancellationToken>()))
                 .Callback<SessionEvent, CancellationToken>((e, _) => captured = e)
                 .Returns(Task.CompletedTask);

        var evt = new SessionAuditIntegrationEvent(
            sessionId, "El equipo respondió", "Equipo Alfa",
            "SubmitTriviaAnswerCommand", SessionEvent.OutcomeSuccess, DateTime.UtcNow);

        var ctx = new Mock<ConsumeContext<SessionAuditIntegrationEvent>>();
        ctx.SetupGet(c => c.Message).Returns(evt);
        ctx.SetupGet(c => c.CancellationToken).Returns(cts.Token);

        await _consumer.Consume(ctx.Object);

        captured.Should().NotBeNull();
        captured!.SessionId.Should().Be(sessionId);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<SessionEvent>(), cts.Token), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(cts.Token), Times.Once);
    }
}
