namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Commands.ForceAdvanceTeam;
using SessionService.Domain.Sessions;
using SessionService.Domain.Statistics;
using SessionService.Infrastructure.Hubs;
using Xunit;

public class ForceAdvanceTeamCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ITeamServiceClient> _teamClientMock = new();
    private readonly Mock<IStageServiceClient> _stageClientMock = new();
    private readonly Mock<ISessionEventRepository> _eventRepoMock = new();
    private readonly Mock<IStageCompletionRecordRepository> _statsRepoMock = new();
    private readonly Mock<IHubContext<SessionHub>> _hubMock = new();
    private readonly ForceAdvanceTeamCommandHandler _handler;

    public ForceAdvanceTeamCommandHandlerTests()
    {
        var clientsMock = new Mock<IHubClients>();
        var proxyMock = new Mock<IClientProxy>();
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(proxyMock.Object);
        _hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        _handler = new ForceAdvanceTeamCommandHandler(
            _sessionRepoMock.Object,
            _teamClientMock.Object,
            _stageClientMock.Object,
            _eventRepoMock.Object,
            _statsRepoMock.Object,
            _hubMock.Object);
    }

    private static Session CreateSessionWithStatus(SessionStatus status)
    {
        var session = Session.Create(Guid.NewGuid(), "Test").Value;
        typeof(Session).GetProperty(nameof(Session.Status))!.SetValue(session, status);
        return session;
    }

    // ── Session not found ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionNotFound_ReturnsNotFoundError()
    {
        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync((Session?)null);

        var result = await _handler.Handle(new ForceAdvanceTeamCommand(Guid.NewGuid(), Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
    }

    // ── Session not InProgress ────────────────────────────────────────────────

    [Theory]
    [InlineData(SessionStatus.Pending)]
    [InlineData(SessionStatus.Paused)]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Cancelled)]
    public async Task Handle_WhenSessionNotInProgress_ReturnsCannotForceAdvanceError(SessionStatus status)
    {
        var session = CreateSessionWithStatus(status);
        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(session);

        var result = await _handler.Handle(new ForceAdvanceTeamCommand(session.Id, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.CannotForceAdvance);
        _teamClientMock.Verify(t => t.GetTeamProgressAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Team not enrolled ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTeamNotInSession_ReturnsTeamNotFoundError()
    {
        var session = CreateSessionWithStatus(SessionStatus.InProgress);
        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(session);
        _teamClientMock.Setup(t => t.GetTeamProgressAsync(session.Id, It.IsAny<CancellationToken>()))
                       .ReturnsAsync([]); // no teams

        var result = await _handler.Handle(new ForceAdvanceTeamCommand(session.Id, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.TeamNotFound);
    }

    // ── Team already on last stage ────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTeamOnLastStage_ReturnsAlreadyOnLastStageError()
    {
        var session = CreateSessionWithStatus(SessionStatus.InProgress);
        var teamId = Guid.NewGuid();

        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(session);
        _teamClientMock.Setup(t => t.GetTeamProgressAsync(session.Id, It.IsAny<CancellationToken>()))
                       .ReturnsAsync([new TeamProgressItem(teamId, "Alpha", 3, 0, null, false)]);
        _stageClientMock.Setup(s => s.GetStagesByMissionAsync(session.MissionId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync([new StageInfo(Guid.NewGuid(), 1), new StageInfo(Guid.NewGuid(), 2), new StageInfo(Guid.NewGuid(), 3)]);

        var result = await _handler.Handle(new ForceAdvanceTeamCommand(session.Id, teamId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.TeamAlreadyOnLastStage);
        _teamClientMock.Verify(t => t.ForceAdvanceTeamAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidAdvance_AdvancesLogsAuditAndBroadcasts()
    {
        var session = CreateSessionWithStatus(SessionStatus.InProgress);
        var teamId = Guid.NewGuid();

        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(session);
        _teamClientMock.Setup(t => t.GetTeamProgressAsync(session.Id, It.IsAny<CancellationToken>()))
                       .ReturnsAsync([new TeamProgressItem(teamId, "Beta Team", 1, 0, null, false)]);
        _stageClientMock.Setup(s => s.GetStagesByMissionAsync(session.MissionId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync([new StageInfo(Guid.NewGuid(), 1), new StageInfo(Guid.NewGuid(), 2), new StageInfo(Guid.NewGuid(), 3)]);
        _teamClientMock.Setup(t => t.ForceAdvanceTeamAsync(teamId, 2, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new StageTransitionResult(NewScore: 0, ElapsedSeconds: 120));

        var result = await _handler.Handle(new ForceAdvanceTeamCommand(session.Id, teamId), default);

        result.IsSuccess.Should().BeTrue();
        _teamClientMock.Verify(t => t.ForceAdvanceTeamAsync(teamId, 2, It.IsAny<CancellationToken>()), Times.Once);
        _eventRepoMock.Verify(e => e.AddAsync(It.IsAny<SessionEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _eventRepoMock.Verify(e => e.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _hubMock.Verify(h => h.Clients.Group(session.Id.ToString()), Times.Once);

        // HU-25: a force-advance row must be persisted with WasCorrect=null
        // and WasForceAdvance=true. The stage clock returned 120s.
        _statsRepoMock.Verify(
            r => r.AddAsync(
                It.Is<StageCompletionRecord>(rec =>
                    rec.WasForceAdvance == true
                    && rec.WasCorrect == null
                    && rec.ElapsedSeconds == 120
                    && rec.StageOrder == 1), // the stage being abandoned
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Error paths: no analytics row is written ──────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionNotInProgress_DoesNotRecordAnalyticsRow()
    {
        var session = CreateSessionWithStatus(SessionStatus.Paused);
        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(session);

        var result = await _handler.Handle(new ForceAdvanceTeamCommand(session.Id, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        _statsRepoMock.Verify(
            r => r.AddAsync(It.IsAny<StageCompletionRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
