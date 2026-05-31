namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Commands.StartSession;
using SessionService.Domain.Sessions;
using SessionService.Infrastructure.Hubs;
using Xunit;

public class StartSessionCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ITeamServiceClient> _teamClientMock = new();
    private readonly Mock<ISessionEventRepository> _eventRepoMock = new();
    private readonly Mock<IHubContext<SessionHub>> _hubMock = new();
    private readonly StartSessionCommandHandler _handler;

    public StartSessionCommandHandlerTests()
    {
        var clientsMock = new Mock<IHubClients>();
        var proxyMock = new Mock<IClientProxy>();
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(proxyMock.Object);
        _hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        _handler = new StartSessionCommandHandler(
            _sessionRepoMock.Object,
            _teamClientMock.Object,
            _eventRepoMock.Object,
            _hubMock.Object);
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
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sesión a iniciar").Value;

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
        _hubMock.Verify(h => h.Clients.Group(sessionId.ToString()), Times.Once);
        _eventRepoMock.Verify(
            r => r.AddAsync(It.Is<SessionEvent>(e => e.Description.Contains("iniciada")), It.IsAny<CancellationToken>()),
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

        SessionEvent? captured = null;
        _eventRepoMock
            .Setup(r => r.AddAsync(It.IsAny<SessionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<SessionEvent, CancellationToken>((ev, _) => captured = ev);

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
