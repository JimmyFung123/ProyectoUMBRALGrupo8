namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Commands.FinalizeSession;
using SessionService.Domain.Sessions;
using SessionService.Domain.Statistics;
using Xunit;

public class FinalizeSessionCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<IIntegrationEventBus> _busMock = new();
    private readonly Mock<IStageCompletionRecordRepository> _statsRepoMock = new();
    private readonly Mock<ISessionNotifier> _notifierMock = new();
    private readonly FinalizeSessionCommandHandler _handler;

    public FinalizeSessionCommandHandlerTests()
    {
        _handler = new FinalizeSessionCommandHandler(
            _sessionRepoMock.Object,
            _busMock.Object,
            _statsRepoMock.Object,
            _notifierMock.Object);
    }

    // ── Session not found ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionNotFound_ReturnsNotFoundError()
    {
        var sessionId = Guid.NewGuid();
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var result = await _handler.Handle(new FinalizeSessionCommand(sessionId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Invalid transitions ───────────────────────────────────────────────────

    [Theory]
    [InlineData(SessionStatus.Pending)]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Cancelled)]
    public async Task Handle_WhenSessionNotActiveOrPaused_ReturnsCannotFinalizeError(SessionStatus status)
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSessionWithStatus(status);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _handler.Handle(new FinalizeSessionCommand(sessionId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.CannotFinalizeSession);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);

        // HU-25: when finalize fails, the analytics rows MUST stay invisible.
        _statsRepoMock.Verify(
            r => r.MarkSessionIncludedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Happy path: finalize from InProgress ──────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionIsInProgress_FinalizesAndBroadcasts()
    {
        var session = CreateSessionWithStatus(SessionStatus.InProgress);
        var sessionId = session.Id;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _handler.Handle(new FinalizeSessionCommand(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(SessionStatus.Completed);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notifierMock.Verify(n => n.NotifyStateChangedAsync(sessionId, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);

        // HU-25: every record of this session must be promoted to dashboard visibility.
        _statsRepoMock.Verify(
            r => r.MarkSessionIncludedAsync(sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Happy path: finalize from Paused ──────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionIsPaused_FinalizesAndBroadcasts()
    {
        var session = CreateSessionWithStatus(SessionStatus.Paused);
        var sessionId = session.Id;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _handler.Handle(new FinalizeSessionCommand(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(SessionStatus.Completed);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notifierMock.Verify(n => n.NotifyStateChangedAsync(sessionId, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static Session CreateSessionWithStatus(SessionStatus status)
    {
        var session = Session.Create(Guid.NewGuid(), "Test").Value;
        typeof(Session)
            .GetProperty(nameof(Session.Status))!
            .SetValue(session, status);
        return session;
    }
}
