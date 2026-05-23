namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application.Sessions.Commands.CancelSession;
using SessionService.Domain.Sessions;
using Xunit;

public class CancelSessionCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ITeamRepository> _teamRepoMock = new();
    private readonly CancelSessionCommandHandler _handler;

    public CancelSessionCommandHandlerTests()
    {
        _handler = new CancelSessionCommandHandler(
            _sessionRepoMock.Object,
            _teamRepoMock.Object);
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
        _teamRepoMock.Verify(
            r => r.DeleteBySessionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Flujo feliz sin equipos ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionIsPendingWithNoTeams_CancelsAndSaves()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sesión sin equipos").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _handler.Handle(new CancelSessionCommand(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(SessionStatus.Cancelled);
        _teamRepoMock.Verify(
            r => r.DeleteBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Flujo feliz con equipos ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionHasTeams_DeletesTeamsBeforeSaving()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sesión con equipos").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var callOrder = new List<string>();

        _teamRepoMock
            .Setup(r => r.DeleteBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("DeleteTeams"))
            .Returns(Task.CompletedTask);

        _sessionRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("SaveChanges"))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(new CancelSessionCommand(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        callOrder.Should().Equal("DeleteTeams", "SaveChanges");
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
