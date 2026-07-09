namespace UMBRAL_Back_end.Tests.Infrastructure.Messaging;

using MassTransit;
using Moq;
using TeamService.Application.Rankings;
using TeamService.Domain.Teams;
using TeamService.Infrastructure.Messaging.Consumers;
using UMBRAL.Contracts.Events;
using Xunit;

public class SessionCancelledConsumerTests
{
    private readonly Mock<ITeamRepository> _repoMock = new();
    private readonly Mock<IRankingProjector> _projectorMock = new();
    private readonly SessionCancelledConsumer _consumer;

    public SessionCancelledConsumerTests()
        => _consumer = new SessionCancelledConsumer(_repoMock.Object, _projectorMock.Object);

    private static Mock<ConsumeContext<SessionCancelledIntegrationEvent>> ContextFor(
        Guid sessionId, CancellationToken ct = default)
    {
        var ctx = new Mock<ConsumeContext<SessionCancelledIntegrationEvent>>();
        ctx.SetupGet(c => c.Message)
           .Returns(new SessionCancelledIntegrationEvent(sessionId, DateTime.UtcNow));
        ctx.SetupGet(c => c.CancellationToken).Returns(ct);
        return ctx;
    }

    [Fact]
    public async Task Consume_DeletesTeamsOfTheCancelledSession()
    {
        var sessionId = Guid.NewGuid();

        await _consumer.Consume(ContextFor(sessionId).Object);

        _repoMock.Verify(r => r.DeleteBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_RemovesRankingProjectionOfTheSession()
    {
        var sessionId = Guid.NewGuid();

        await _consumer.Consume(ContextFor(sessionId).Object);

        // HU-24: the read model must drop the cancelled session's rows too.
        _projectorMock.Verify(p => p.RemoveSessionAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_PersistsChangesOnce()
    {
        await _consumer.Consume(ContextFor(Guid.NewGuid()).Object);

        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_ForwardsTheContextCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var sessionId = Guid.NewGuid();

        await _consumer.Consume(ContextFor(sessionId, cts.Token).Object);

        _repoMock.Verify(r => r.DeleteBySessionIdAsync(sessionId, cts.Token), Times.Once);
        _projectorMock.Verify(p => p.RemoveSessionAsync(sessionId, cts.Token), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(cts.Token), Times.Once);
    }
}
