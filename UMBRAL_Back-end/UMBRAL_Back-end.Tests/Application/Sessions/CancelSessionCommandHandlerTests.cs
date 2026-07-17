namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application;
using SessionService.Application.Sessions.Commands.CancelSession;
using SessionService.Domain.Sessions;
using SessionService.Domain.Sessions.Events;
using Xunit;

public class CancelSessionCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly CancelSessionCommandHandler _handler;

    public CancelSessionCommandHandlerTests()
    {
        _handler = new CancelSessionCommandHandler(_sessionRepoMock.Object);
    }

    // ── Sesión no encontrada ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionNotFound_ReturnsNotFoundError()
    {
        var sessionId = Guid.NewGuid();

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var result = await _handler.Handle(new CancelSessionCommand(sessionId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Estado no cancelable ──────────────────────────────────────────────────

    [Theory]
    [InlineData(SessionStatus.InProgress)]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Cancelled)]
    public async Task Handle_WhenSessionIsNotPending_ReturnsCannotCancelError(SessionStatus status)
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSessionWithStatus(status);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _handler.Handle(new CancelSessionCommand(sessionId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.CannotCancelNonPendingSession);
        session.DomainEvents.OfType<SessionCancelledDomainEvent>().Should().BeEmpty();
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Flujo feliz ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionIsPending_CancelsAndRaisesDomainEvent()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sesión cancelable").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _handler.Handle(new CancelSessionCommand(sessionId, "Op. Juan"), default);

        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(SessionStatus.Cancelled);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        var raised = session.DomainEvents.OfType<SessionCancelledDomainEvent>().Should().ContainSingle().Which;
        raised.SessionId.Should().Be(session.Id);
        raised.OperatorName.Should().Be("Op. Juan");
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static Session CreateSessionWithStatus(SessionStatus status)
    {
        var session = Session.Create(Guid.NewGuid(), "Sesión de prueba").Value;
        typeof(Session)
            .GetProperty(nameof(Session.Status))!
            .SetValue(session, status);
        return session;
    }
}
