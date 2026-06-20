namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Commands.StartSession;
using SessionService.Domain.Sessions;
using UMBRAL.Contracts.Events;
using Xunit;

public class StartSessionCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ITeamServiceClient> _teamClientMock = new();
    private readonly Mock<IIntegrationEventBus> _busMock = new();
    private readonly Mock<ISessionNotifier> _notifierMock = new();
    private readonly StartSessionCommandHandler _handler;

    public StartSessionCommandHandlerTests()
    {
        _handler = new StartSessionCommandHandler(
            _sessionRepoMock.Object,
            _teamClientMock.Object,
            _busMock.Object,
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

        var result = await _handler.Handle(new StartSessionCommand(sessionId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        // TeamService should not be called if session doesn't exist
        _teamClientMock.Verify(
            t => t.HasEnrolledTeamsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── HU-12: no teams enrolled ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNoTeamsEnrolled_ReturnsNoTeamsEnrolledError()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sesión sin equipos").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _teamClientMock
            .Setup(t => t.HasEnrolledTeamsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Handle(new StartSessionCommand(sessionId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NoTeamsEnrolled);
        session.Status.Should().Be(SessionStatus.Pending); // unchanged
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTeamServiceUnreachable_ReturnsNoTeamsEnrolledError()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sesión service caído").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // TeamServiceClient catches exceptions and returns false
        _teamClientMock
            .Setup(t => t.HasEnrolledTeamsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Handle(new StartSessionCommand(sessionId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NoTeamsEnrolled);
    }

    // ── Invalid state transitions ─────────────────────────────────────────────

    [Theory]
    [InlineData(SessionStatus.InProgress)]
    [InlineData(SessionStatus.Paused)]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Cancelled)]
    public async Task Handle_WhenSessionNotPending_ReturnsCannotStartError(SessionStatus status)
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSessionWithStatus(status);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _teamClientMock
            .Setup(t => t.HasEnrolledTeamsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _teamClientMock
            .Setup(t => t.AllTeamsMeetMinimumMembersAsync(sessionId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.Handle(new StartSessionCommand(sessionId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.CannotStartSession);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── RB-18: minimum 2 members per team ────────────────────────────────────

    [Fact]
    public async Task Handle_WhenATeamHasFewerThanTwoMembers_ReturnsTeamBelowMinimumMembersError()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sesión con equipo incompleto").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _teamClientMock
            .Setup(t => t.HasEnrolledTeamsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _teamClientMock
            .Setup(t => t.AllTeamsMeetMinimumMembersAsync(sessionId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Handle(new StartSessionCommand(sessionId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.TeamBelowMinimumMembers);
        session.Status.Should().Be(SessionStatus.Pending);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRb18CheckFails_DoesNotCheckSessionState()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sesión RB-18").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _teamClientMock
            .Setup(t => t.HasEnrolledTeamsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _teamClientMock
            .Setup(t => t.AllTeamsMeetMinimumMembersAsync(sessionId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Handle(new StartSessionCommand(sessionId), default);

        result.Error.Should().Be(SessionErrors.TeamBelowMinimumMembers);
        // session.Start() was never called — state is still Pending
        session.Status.Should().Be(SessionStatus.Pending);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenPendingAndAllTeamsHaveEnoughMembers_StartsAndBroadcasts()
    {
        var session = Session.Create(Guid.NewGuid(), "Sesión a iniciar").Value;
        var sessionId = session.Id;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _teamClientMock
            .Setup(t => t.HasEnrolledTeamsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _teamClientMock
            .Setup(t => t.AllTeamsMeetMinimumMembersAsync(sessionId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.Handle(new StartSessionCommand(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(SessionStatus.InProgress);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notifierMock.Verify(n => n.NotifyStateChangedAsync(sessionId, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(
            b => b.PublishAsync(
                It.Is<SessionAuditIntegrationEvent>(e => e.Description.Contains("iniciada")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOperatorNameProvided_RecordsAuditWithThatActor()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sesión auditada").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _teamClientMock
            .Setup(t => t.HasEnrolledTeamsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _teamClientMock
            .Setup(t => t.AllTeamsMeetMinimumMembersAsync(sessionId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        SessionAuditIntegrationEvent? captured = null;
        _busMock
            .Setup(b => b.PublishAsync(It.IsAny<SessionAuditIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<SessionAuditIntegrationEvent, CancellationToken>((ev, _) => captured = ev);

        await _handler.Handle(new StartSessionCommand(sessionId, OperatorName: "Prof. Ortega"), default);

        captured.Should().NotBeNull();
        captured!.ActorName.Should().Be("Prof. Ortega");
    }

    // ── Validation order: NotFound > NoTeams > CannotStart ───────────────────

    [Fact]
    public async Task Handle_ValidationOrder_NotFoundCheckedBeforeTeams()
    {
        var sessionId = Guid.NewGuid();
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        await _handler.Handle(new StartSessionCommand(sessionId), default);

        // TeamService must NOT be called if session not found
        _teamClientMock.Verify(
            t => t.HasEnrolledTeamsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
