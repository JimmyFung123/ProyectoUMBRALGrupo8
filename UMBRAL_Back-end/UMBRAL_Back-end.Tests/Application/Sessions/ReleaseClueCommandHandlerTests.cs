namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Commands.ReleaseClue;
using SessionService.Domain.Sessions;
using SessionService.Infrastructure.Hubs;
using Xunit;

public class ReleaseClueCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ITeamServiceClient> _teamClientMock = new();
    private readonly Mock<IHubContext<SessionHub>> _hubMock = new();
    private readonly ReleaseClueCommandHandler _handler;

    public ReleaseClueCommandHandlerTests()
    {
        var clientsMock = new Mock<IHubClients>();
        var proxyMock = new Mock<IClientProxy>();
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(proxyMock.Object);
        _hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        _handler = new ReleaseClueCommandHandler(
            _sessionRepoMock.Object,
            _teamClientMock.Object,
            _hubMock.Object);
    }

    private static ReleaseClueCommand MakeCommand(Guid sessionId, Guid teamId)
        => new(sessionId, teamId, TotalCluesForStage: 3, ClueContent: "Busca la fuente de la plaza");

    // ── Session not found ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionNotFound_ReturnsNotFoundError()
    {
        var cmd = MakeCommand(Guid.NewGuid(), Guid.NewGuid());
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(cmd.SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var result = await _handler.Handle(cmd, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
        _teamClientMock.Verify(
            t => t.ReleaseClueAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Session not InProgress ────────────────────────────────────────────────

    [Theory]
    [InlineData(SessionStatus.Pending)]
    [InlineData(SessionStatus.Paused)]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Cancelled)]
    public async Task Handle_WhenSessionNotInProgress_ReturnsCannotReleaseError(SessionStatus status)
    {
        var cmd = MakeCommand(Guid.NewGuid(), Guid.NewGuid());
        var session = CreateSessionWithStatus(status);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(cmd.SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _handler.Handle(cmd, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.CannotReleaseClue);
        _teamClientMock.Verify(
            t => t.ReleaseClueAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── All clues exhausted ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenAllCluesExhausted_ReturnsAllCluesReleasedError()
    {
        var cmd = MakeCommand(Guid.NewGuid(), Guid.NewGuid());
        var session = CreateSessionWithStatus(SessionStatus.InProgress);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(cmd.SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // TeamService signals exhaustion with -1
        _teamClientMock
            .Setup(t => t.ReleaseClueAsync(cmd.TeamId, cmd.TotalCluesForStage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(-1);

        var result = await _handler.Handle(cmd, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.AllCluesAlreadyReleased);
        _hubMock.Verify(h => h.Clients, Times.Never);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionInProgressAndCluesAvailable_BroadcastsAndSucceeds()
    {
        var cmd = MakeCommand(Guid.NewGuid(), Guid.NewGuid());
        var session = CreateSessionWithStatus(SessionStatus.InProgress);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(cmd.SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _teamClientMock
            .Setup(t => t.ReleaseClueAsync(cmd.TeamId, cmd.TotalCluesForStage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1); // first clue released

        var result = await _handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        _hubMock.Verify(h => h.Clients.Group(cmd.SessionId.ToString()), Times.Once);
    }

    // ── Validation order: NotFound before TeamService call ────────────────────

    [Fact]
    public async Task Handle_WhenSessionNotFound_TeamServiceNeverCalled()
    {
        var cmd = MakeCommand(Guid.NewGuid(), Guid.NewGuid());
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(cmd.SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        await _handler.Handle(cmd, default);

        _teamClientMock.Verify(
            t => t.ReleaseClueAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static Session CreateSessionWithStatus(SessionStatus status)
    {
        var session = Session.Create(Guid.NewGuid(), "Test session").Value;
        typeof(Session)
            .GetProperty(nameof(Session.Status))!
            .SetValue(session, status);
        return session;
    }
}
