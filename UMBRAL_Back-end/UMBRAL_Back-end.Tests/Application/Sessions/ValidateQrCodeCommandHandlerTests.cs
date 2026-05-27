namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Commands.ValidateQrCode;
using SessionService.Domain.Sessions;
using SessionService.Domain.Statistics;
using SessionService.Infrastructure.Hubs;
using Xunit;

public class ValidateQrCodeCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ITeamServiceClient> _teamClientMock = new();
    private readonly Mock<IStageServiceClient> _stageClientMock = new();
    private readonly Mock<ISessionEventRepository> _eventRepoMock = new();
    private readonly Mock<IStageCompletionRecordRepository> _statsRepoMock = new();
    private readonly Mock<IHubContext<SessionHub>> _hubMock = new();
    private readonly ValidateQrCodeCommandHandler _handler;

    public ValidateQrCodeCommandHandlerTests()
    {
        var clientsMock = new Mock<IHubClients>();
        var proxyMock = new Mock<IClientProxy>();
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(proxyMock.Object);
        _hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        _handler = new ValidateQrCodeCommandHandler(
            _sessionRepoMock.Object,
            _teamClientMock.Object,
            _stageClientMock.Object,
            _eventRepoMock.Object,
            _statsRepoMock.Object,
            _hubMock.Object);
    }

    // ── Session not found ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionNotFound_ReturnsNotFoundError()
    {
        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync((Session?)null);

        var result = await _handler.Handle(
            new ValidateQrCodeCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QR-X"),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
    }

    // ── Session paused: cannot accept evidence (RB-03) ────────────────────────

    [Fact]
    public async Task Handle_WhenSessionPaused_ReturnsNotInProgressError()
    {
        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(CreateSessionWithStatus(SessionStatus.Paused));

        var result = await _handler.Handle(
            new ValidateQrCodeCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QR-X"),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotInProgress);
        _stageClientMock.Verify(
            s => s.GetStageWithOptionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Stage not a TreasureHunt ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenStageIsTrivia_ReturnsStageIsNotTreasureHuntError()
    {
        var stageId = Guid.NewGuid();
        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(CreateSessionWithStatus(SessionStatus.InProgress));
        _stageClientMock.Setup(s => s.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new StageWithOptionsInfo(
                            stageId, "Trivia stage", "Trivia", 1, 50, "Q?",
                            Array.Empty<TriviaOptionInfo>()));

        var result = await _handler.Handle(
            new ValidateQrCodeCommand(Guid.NewGuid(), Guid.NewGuid(), stageId, "QR-X"),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.StageIsNotTreasureHunt);
    }

    // ── Stage has no QR configured ────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenStageHasNoQrCode_ReturnsMissingQrCodeError()
    {
        var stageId = Guid.NewGuid();
        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(CreateSessionWithStatus(SessionStatus.InProgress));
        _stageClientMock.Setup(s => s.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new StageWithOptionsInfo(
                            stageId, "TH stage", "TreasureHunt", 1, 50, null,
                            Array.Empty<TriviaOptionInfo>(),
                            10.0, -66.0, QrCode: null));

        var result = await _handler.Handle(
            new ValidateQrCodeCommand(Guid.NewGuid(), Guid.NewGuid(), stageId, "QR-X"),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.StageMissingQrCode);
    }

    // ── Wrong QR: team stays on stage, no points, no broadcast ────────────────

    [Fact]
    public async Task Handle_WhenScannedCodeDoesNotMatch_ReturnsIncorrectAndKeepsTeamOnStage()
    {
        var stageId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(CreateSessionWithStatus(SessionStatus.InProgress));
        _stageClientMock.Setup(s => s.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new StageWithOptionsInfo(
                            stageId, "TH stage", "TreasureHunt", 1, 50, null,
                            Array.Empty<TriviaOptionInfo>(),
                            10.0, -66.0, "QR-CORRECT"));
        _teamClientMock.Setup(t => t.GetTeamByIdAsync(teamId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new TeamInfoItem(teamId, "Alfa", CurrentStageOrder: 1));

        var result = await _handler.Handle(
            new ValidateQrCodeCommand(Guid.NewGuid(), teamId, stageId, "QR-WRONG"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCorrect.Should().BeFalse();
        result.Value.NewScore.Should().Be(0);
        result.Value.NextStageOrder.Should().Be(1);
        result.Value.IsLastStage.Should().BeFalse();

        _teamClientMock.Verify(
            t => t.AnswerTriviaAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _hubMock.Verify(h => h.Clients.Group(It.IsAny<string>()), Times.Never);
        _eventRepoMock.Verify(
            r => r.AddAsync(It.IsAny<SessionEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // HU-25: a wrong QR scan does NOT complete the stage — no analytics row.
        _statsRepoMock.Verify(
            r => r.AddAsync(It.IsAny<StageCompletionRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Correct QR: advances team + adds points + broadcasts ──────────────────

    [Fact]
    public async Task Handle_WhenScannedCodeMatches_AdvancesTeamAndBroadcasts()
    {
        var stageId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(CreateSessionWithStatus(SessionStatus.InProgress));
        _stageClientMock.Setup(s => s.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new StageWithOptionsInfo(
                            stageId, "TH stage", "TreasureHunt", 1, 75, null,
                            Array.Empty<TriviaOptionInfo>(),
                            10.0, -66.0, "QR-CORRECT"));
        _stageClientMock.Setup(s => s.GetStagesByMissionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new[]
                        {
                            new StageInfo(stageId, 1),
                            new StageInfo(Guid.NewGuid(), 2),
                        });
        _teamClientMock.Setup(t => t.AnswerTriviaAsync(teamId, true, 75, 2, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new StageTransitionResult(NewScore: 175, ElapsedSeconds: 90));
        _teamClientMock.Setup(t => t.GetTeamByIdAsync(teamId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new TeamInfoItem(teamId, "Alfa", CurrentStageOrder: 1));

        var result = await _handler.Handle(
            new ValidateQrCodeCommand(Guid.NewGuid(), teamId, stageId, "QR-CORRECT"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCorrect.Should().BeTrue();
        result.Value.NewScore.Should().Be(175);
        result.Value.NextStageOrder.Should().Be(2);
        result.Value.IsLastStage.Should().BeFalse();

        _teamClientMock.Verify(
            t => t.AnswerTriviaAsync(teamId, true, 75, 2, It.IsAny<CancellationToken>()),
            Times.Once);
        _hubMock.Verify(h => h.Clients.Group(It.IsAny<string>()), Times.Once);
        _eventRepoMock.Verify(
            r => r.AddAsync(It.IsAny<SessionEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // HU-25: a TreasureHunt/WasCorrect=true record must be appended.
        _statsRepoMock.Verify(
            r => r.AddAsync(
                It.Is<StageCompletionRecord>(rec =>
                    rec.StageType == "TreasureHunt"
                    && rec.WasCorrect == true
                    && rec.WasForceAdvance == false
                    && rec.ElapsedSeconds == 90),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Correct QR on the last stage: IsLastStage = true ──────────────────────

    [Fact]
    public async Task Handle_WhenScannedCodeMatchesOnLastStage_FlagsIsLastStage()
    {
        var stageId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(CreateSessionWithStatus(SessionStatus.InProgress));
        _stageClientMock.Setup(s => s.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new StageWithOptionsInfo(
                            stageId, "Last TH", "TreasureHunt", 3, 100, null,
                            Array.Empty<TriviaOptionInfo>(),
                            10.0, -66.0, "QR-LAST"));
        _stageClientMock.Setup(s => s.GetStagesByMissionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new[]
                        {
                            new StageInfo(Guid.NewGuid(), 1),
                            new StageInfo(Guid.NewGuid(), 2),
                            new StageInfo(stageId, 3),
                        });
        _teamClientMock.Setup(t => t.AnswerTriviaAsync(teamId, true, 100, 4, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new StageTransitionResult(NewScore: 300, ElapsedSeconds: 200));
        _teamClientMock.Setup(t => t.GetTeamByIdAsync(teamId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new TeamInfoItem(teamId, "Beta", CurrentStageOrder: 3));

        var result = await _handler.Handle(
            new ValidateQrCodeCommand(Guid.NewGuid(), teamId, stageId, "QR-LAST"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsLastStage.Should().BeTrue();
        result.Value.NextStageOrder.Should().Be(4);
    }

    // ── QR comparison is case-insensitive and trims whitespace ────────────────

    [Fact]
    public async Task Handle_WhenScannedCodeMatchesIgnoringCaseAndWhitespace_ReturnsCorrect()
    {
        var stageId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(CreateSessionWithStatus(SessionStatus.InProgress));
        _stageClientMock.Setup(s => s.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new StageWithOptionsInfo(
                            stageId, "TH stage", "TreasureHunt", 1, 50, null,
                            Array.Empty<TriviaOptionInfo>(),
                            10.0, -66.0, "qr-abc-123"));
        _stageClientMock.Setup(s => s.GetStagesByMissionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new[] { new StageInfo(stageId, 1) });
        _teamClientMock.Setup(t => t.AnswerTriviaAsync(teamId, true, 50, 2, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new StageTransitionResult(NewScore: 50, ElapsedSeconds: 30));
        _teamClientMock.Setup(t => t.GetTeamByIdAsync(teamId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new TeamInfoItem(teamId, "Alfa", CurrentStageOrder: 1));

        var result = await _handler.Handle(
            new ValidateQrCodeCommand(Guid.NewGuid(), teamId, stageId, "  QR-ABC-123  "),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCorrect.Should().BeTrue();
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
