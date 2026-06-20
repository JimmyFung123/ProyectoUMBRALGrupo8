namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SessionService.Application;
using SessionService.Application.Sessions;
using SessionService.Domain.Sessions;
using Xunit;

public class ClueAutoReleaseServiceTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ITeamServiceClient> _teamClientMock = new();
    private readonly Mock<IStageServiceClient> _stageClientMock = new();
    private readonly Mock<IClueServiceClient> _clueClientMock = new();
    private readonly Mock<IIntegrationEventBus> _busMock = new();
    private readonly Mock<ISessionNotifier> _notifierMock = new();
    private readonly ClueAutoReleaseService _service;

    public ClueAutoReleaseServiceTests()
    {
        _service = new ClueAutoReleaseService(
            _sessionRepoMock.Object,
            _teamClientMock.Object,
            _stageClientMock.Object,
            _clueClientMock.Object,
            _busMock.Object,
            _notifierMock.Object,
            NullLogger<ClueAutoReleaseService>.Instance);
    }

    // ── No sessions in progress ────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_NoSessions_DoesNothing()
    {
        _sessionRepoMock.Setup(r => r.GetAllInProgressAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _service.ProcessAsync(default);

        _teamClientMock.Verify(t => t.GetTeamProgressAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Stage has no auto-release configured ─────────────────────────────────

    [Fact]
    public async Task ProcessAsync_NoAutoReleaseOnStage_DoesNotRelease()
    {
        var session = CreateInProgressSession();
        var sessionId = session.Id;
        var teamId = Guid.NewGuid();
        var stageId = Guid.NewGuid();

        _sessionRepoMock.Setup(r => r.GetAllInProgressAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);
        _teamClientMock.Setup(t => t.GetTeamProgressAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TeamProgressItem(teamId, "Alpha", 1, 0, DateTime.UtcNow.AddMinutes(-10), false)]);
        _stageClientMock.Setup(s => s.GetStagesByMissionAsync(session.MissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StageInfo(stageId, 1, AutoReleaseTimeMinutes: null)]);

        await _service.ProcessAsync(default);

        _clueClientMock.Verify(c => c.GetCluesByStageAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _teamClientMock.Verify(t => t.ReleaseClueAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()), Times.Never);
    }

    // ── Timer not yet expired ──────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_TimerNotExpired_DoesNotRelease()
    {
        var session = CreateInProgressSession();
        var stageId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        _sessionRepoMock.Setup(r => r.GetAllInProgressAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);
        _teamClientMock.Setup(t => t.GetTeamProgressAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TeamProgressItem(teamId, "Beta", 1, 0, DateTime.UtcNow.AddMinutes(-1), false)]);
        _stageClientMock.Setup(s => s.GetStagesByMissionAsync(session.MissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StageInfo(stageId, 1, AutoReleaseTimeMinutes: 5)]);
        _clueClientMock.Setup(c => c.GetCluesByStageAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ClueInfo(Guid.NewGuid(), 1, "Find it", null, null, null, AutoReleaseAfterMinutes: null)]);

        await _service.ProcessAsync(default);

        _teamClientMock.Verify(t => t.ReleaseClueAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()), Times.Never);
    }

    // ── Happy path: timer expired → releases and broadcasts ───────────────────

    [Fact]
    public async Task ProcessAsync_TimerExpired_ReleasesAndBroadcasts()
    {
        var session = CreateInProgressSession();
        var stageId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var clueId = Guid.NewGuid();

        _sessionRepoMock.Setup(r => r.GetAllInProgressAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);
        _teamClientMock.Setup(t => t.GetTeamProgressAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TeamProgressItem(teamId, "Gamma", 1, 0, DateTime.UtcNow.AddMinutes(-6), false)]);
        _stageClientMock.Setup(s => s.GetStagesByMissionAsync(session.MissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StageInfo(stageId, 1, AutoReleaseTimeMinutes: 5)]);
        _clueClientMock.Setup(c => c.GetCluesByStageAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ClueInfo(clueId, 1, "Find the fountain", null, null, null, AutoReleaseAfterMinutes: null)]);
        _teamClientMock.Setup(t => t.ReleaseClueAsync(teamId, 1, It.IsAny<CancellationToken>(), true))
            .ReturnsAsync(1);

        await _service.ProcessAsync(default);

        _teamClientMock.Verify(t => t.ReleaseClueAsync(teamId, 1, It.IsAny<CancellationToken>(), true), Times.Once);
        _notifierMock.Verify(n => n.NotifyClueReleasedAsync(
            session.Id, teamId,
            It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<double?>(), It.IsAny<int?>(),
            1, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Team has no timer set (never entered stage) ────────────────────────────

    [Fact]
    public async Task ProcessAsync_NoClueTimerResetAt_SkipsTeam()
    {
        var session = CreateInProgressSession();
        var teamId = Guid.NewGuid();
        var stageId = Guid.NewGuid();

        _sessionRepoMock.Setup(r => r.GetAllInProgressAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);
        _teamClientMock.Setup(t => t.GetTeamProgressAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TeamProgressItem(teamId, "Delta", 1, 0, ClueTimerResetAt: null, false)]);
        _stageClientMock.Setup(s => s.GetStagesByMissionAsync(session.MissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StageInfo(stageId, 1)]);

        await _service.ProcessAsync(default);

        _clueClientMock.Verify(c => c.GetCluesByStageAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _teamClientMock.Verify(t => t.ReleaseClueAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()), Times.Never);
    }

    // ── All clues already released ─────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_AllCluesReleased_SkipsTeam()
    {
        var session = CreateInProgressSession();
        var stageId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        _sessionRepoMock.Setup(r => r.GetAllInProgressAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);
        // CluesReceivedCurrentStage == clues.Count → index out of range → nextClue is null
        _teamClientMock.Setup(t => t.GetTeamProgressAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TeamProgressItem(teamId, "Epsilon", 1, 2, DateTime.UtcNow.AddMinutes(-10), false)]);
        _stageClientMock.Setup(s => s.GetStagesByMissionAsync(session.MissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StageInfo(stageId, 1, AutoReleaseTimeMinutes: 5)]);
        _clueClientMock.Setup(c => c.GetCluesByStageAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ClueInfo(Guid.NewGuid(), 1, "Clue 1", null, null, null, 5),
                new ClueInfo(Guid.NewGuid(), 2, "Clue 2", null, null, null, 5),
            ]);

        await _service.ProcessAsync(default);

        _teamClientMock.Verify(t => t.ReleaseClueAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()), Times.Never);
    }

    // ── Helper ─────────────────────────────────────────────────────────────────

    private static Session CreateInProgressSession()
    {
        var session = Session.Create(Guid.NewGuid(), "Test Session").Value;
        typeof(Session)
            .GetProperty(nameof(Session.Status))!
            .SetValue(session, SessionStatus.InProgress);
        return session;
    }
}
