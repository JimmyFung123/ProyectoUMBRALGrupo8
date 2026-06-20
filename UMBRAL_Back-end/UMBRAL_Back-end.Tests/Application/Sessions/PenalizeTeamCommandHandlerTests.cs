namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Commands.PenalizeTeam;
using SessionService.Domain.Sessions;
using Xunit;

public class PenalizeTeamCommandHandlerSessionTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ITeamServiceClient> _teamClientMock = new();
    private readonly Mock<IIntegrationEventBus> _busMock = new();
    private readonly Mock<ISessionNotifier> _notifierMock = new();
    private readonly PenalizeTeamCommandHandler _handler;

    public PenalizeTeamCommandHandlerSessionTests()
    {
        _handler = new PenalizeTeamCommandHandler(
            _sessionRepoMock.Object,
            _teamClientMock.Object,
            _busMock.Object,
            _notifierMock.Object);
    }

    private static PenalizeTeamCommand MakeCommand(Guid sessionId, Guid teamId)
        => new(sessionId, teamId, Points: 20, Reason: "Misconduct during activity");

    // ── Session not found ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionNotFound_ReturnsNotFoundError()
    {
        var cmd = MakeCommand(Guid.NewGuid(), Guid.NewGuid());
        _sessionRepoMock.Setup(r => r.GetByIdAsync(cmd.SessionId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync((Session?)null);

        var result = await _handler.Handle(cmd, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
        _teamClientMock.Verify(t => t.PenalizeTeamAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Session not InProgress ────────────────────────────────────────────────

    [Theory]
    [InlineData(SessionStatus.Pending)]
    [InlineData(SessionStatus.Paused)]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Cancelled)]
    public async Task Handle_WhenSessionNotInProgress_ReturnsCannotPenalizeError(SessionStatus status)
    {
        var cmd = MakeCommand(Guid.NewGuid(), Guid.NewGuid());
        var session = CreateSessionWithStatus(status);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(cmd.SessionId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(session);

        var result = await _handler.Handle(cmd, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.CannotPenalizeTeam);
        _teamClientMock.Verify(t => t.PenalizeTeamAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Empty reason blocked at handler ───────────────────────────────────────

    [Fact]
    public async Task Handle_WhenReasonEmpty_ReturnsCannotPenalizeError()
    {
        var cmd = new PenalizeTeamCommand(Guid.NewGuid(), Guid.NewGuid(), 10, "  ");
        var result = await _handler.Handle(cmd, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.CannotPenalizeTeam);
        _sessionRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionInProgress_PenalizesAndBroadcasts()
    {
        var cmd = MakeCommand(Guid.NewGuid(), Guid.NewGuid());
        var session = CreateSessionWithStatus(SessionStatus.InProgress);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(cmd.SessionId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(session);
        _teamClientMock.Setup(t => t.PenalizeTeamAsync(cmd.TeamId, cmd.Points, cmd.Reason, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(80); // new score after penalty

        var result = await _handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(80);
        _notifierMock.Verify(n => n.NotifyTeamPenalizedAsync(
            cmd.SessionId, cmd.TeamId, It.IsAny<string>(),
            cmd.Points, cmd.Reason, 80, It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── TeamService failure ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTeamServiceFails_ReturnsCannotPenalizeError()
    {
        var cmd = MakeCommand(Guid.NewGuid(), Guid.NewGuid());
        var session = CreateSessionWithStatus(SessionStatus.InProgress);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(cmd.SessionId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(session);
        _teamClientMock.Setup(t => t.PenalizeTeamAsync(cmd.TeamId, cmd.Points, cmd.Reason, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(int.MinValue); // failure sentinel

        var result = await _handler.Handle(cmd, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.CannotPenalizeTeam);
        _notifierMock.Verify(n => n.NotifyTeamPenalizedAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static Session CreateSessionWithStatus(SessionStatus status)
    {
        var session = Session.Create(Guid.NewGuid(), "Test Session").Value;
        typeof(Session)
            .GetProperty(nameof(Session.Status))!
            .SetValue(session, status);
        return session;
    }
}
