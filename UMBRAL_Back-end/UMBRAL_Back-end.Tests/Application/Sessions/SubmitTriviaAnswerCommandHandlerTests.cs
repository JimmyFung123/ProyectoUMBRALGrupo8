namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Commands.SubmitTriviaAnswer;
using SessionService.Domain.MissionLookup;
using SessionService.Domain.Sessions;
using SessionService.Domain.Statistics;
using UMBRAL.Contracts.Events;
using Xunit;

public class SubmitTriviaAnswerCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ITeamServiceClient> _teamClientMock = new();
    private readonly Mock<IStageServiceClient> _stageClientMock = new();
    private readonly Mock<IStageCompletionRecordRepository> _statsRepoMock = new();
    private readonly Mock<IIntegrationEventBus> _busMock = new();
    private readonly Mock<IMissionLookupRepository> _missionLookupRepoMock = new();
    private readonly SubmitTriviaAnswerCommandHandler _handler;

    public SubmitTriviaAnswerCommandHandlerTests()
    {
        // Default: null lookup → ScoringStrategyFactory defaults to MediumScoringStrategy
        _missionLookupRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MissionLookup?)null);

        _handler = new SubmitTriviaAnswerCommandHandler(
            _sessionRepoMock.Object,
            _teamClientMock.Object,
            _stageClientMock.Object,
            _statsRepoMock.Object,
            _busMock.Object,
            _missionLookupRepoMock.Object);
    }

    // ── Session not found ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionNotFound_ReturnsNotFoundError()
    {
        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync((Session?)null);

        var result = await _handler.Handle(
            new SubmitTriviaAnswerCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
    }

    // ── Session paused ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionPaused_ReturnsNotInProgressError()
    {
        var session = CreateSessionWithStatus(SessionStatus.Paused);
        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(session);

        var result = await _handler.Handle(
            new SubmitTriviaAnswerCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotInProgress);
        // CoR: stage is fetched upfront alongside session before the chain runs
        _stageClientMock.Verify(s => s.GetStageWithOptionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);

        // HU-25: no record is appended when the trivia attempt is rejected.
        _statsRepoMock.Verify(
            r => r.AddAsync(It.IsAny<StageCompletionRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Option not found in stage ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenOptionNotFound_ReturnsOptionNotFoundError()
    {
        var session = CreateSessionWithStatus(SessionStatus.InProgress);
        var stageId = Guid.NewGuid();
        var wrongOptionId = Guid.NewGuid();

        var knownOption = new TriviaOptionInfo(Guid.NewGuid(), "Option A", true);
        var stageInfo = new StageWithOptionsInfo(stageId, "Stage 1", "Trivia", 1, 50, "Question?",
            new[] { knownOption });

        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(session);
        _stageClientMock.Setup(s => s.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(stageInfo);

        var result = await _handler.Handle(
            new SubmitTriviaAnswerCommand(Guid.NewGuid(), Guid.NewGuid(), stageId, wrongOptionId),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.OptionNotFound);
    }

    // ── Correct answer ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CorrectAnswer_CallsRecordEvidenceOutcomeWithIsCorrectTrueAndBroadcasts()
    {
        var session = CreateSessionWithStatus(SessionStatus.InProgress);
        var stageId = Guid.NewGuid();
        var correctOptionId = Guid.NewGuid();

        var correctOption = new TriviaOptionInfo(correctOptionId, "Correct answer", true);
        var stageInfo = new StageWithOptionsInfo(stageId, "Stage 1", "Trivia", 1, 50, "Q?",
            new[] { correctOption });

        var stage1Ref = new StageInfo(stageId, 1);
        var stage2Id = Guid.NewGuid();
        var stage2Ref = new StageInfo(stage2Id, 2);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(session);
        _stageClientMock.Setup(s => s.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(stageInfo);
        _stageClientMock.Setup(s => s.GetStagesByMissionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new[] { stage1Ref, stage2Ref });
        _teamClientMock.Setup(t => t.RecordEvidenceOutcomeAsync(It.IsAny<Guid>(), true, 50, 2, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new StageTransitionResult(NewScore: 150, ElapsedSeconds: 42));

        var result = await _handler.Handle(
            new SubmitTriviaAnswerCommand(Guid.NewGuid(), Guid.NewGuid(), stageId, correctOptionId),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCorrect.Should().BeTrue();
        result.Value.NewScore.Should().Be(150);
        _teamClientMock.Verify(t => t.RecordEvidenceOutcomeAsync(
            It.IsAny<Guid>(), true, 50, 2, It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(
            b => b.PublishAsync(It.IsAny<StageCompletedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // HU-25: a Trivia/WasCorrect=true record must be appended to the fact table.
        _statsRepoMock.Verify(
            r => r.AddAsync(
                It.Is<StageCompletionRecord>(rec =>
                    rec.StageType == "Trivia"
                    && rec.WasCorrect == true
                    && rec.WasForceAdvance == false
                    && rec.ElapsedSeconds == 42
                    && rec.IncludedInStatistics == false), // promoted on finalize
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Incorrect answer ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_IncorrectAnswer_CallsRecordEvidenceOutcomeWithIsCorrectFalse()
    {
        var session = CreateSessionWithStatus(SessionStatus.InProgress);
        var stageId = Guid.NewGuid();
        var wrongOptionId = Guid.NewGuid();

        var wrongOption = new TriviaOptionInfo(wrongOptionId, "Wrong answer", false);
        var stageInfo = new StageWithOptionsInfo(stageId, "Stage 1", "Trivia", 1, 50, "Q?",
            new[] { wrongOption });

        var stage1Ref = new StageInfo(stageId, 1);
        var stage2Ref = new StageInfo(Guid.NewGuid(), 2);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(session);
        _stageClientMock.Setup(s => s.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(stageInfo);
        _stageClientMock.Setup(s => s.GetStagesByMissionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new[] { stage1Ref, stage2Ref });
        // Medium strategy: wrong answer → -(baseScore/2) = -25
        _teamClientMock.Setup(t => t.RecordEvidenceOutcomeAsync(It.IsAny<Guid>(), false, -25, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new StageTransitionResult(NewScore: 25, ElapsedSeconds: 13));

        var result = await _handler.Handle(
            new SubmitTriviaAnswerCommand(Guid.NewGuid(), Guid.NewGuid(), stageId, wrongOptionId),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCorrect.Should().BeFalse();
        _teamClientMock.Verify(t => t.RecordEvidenceOutcomeAsync(
            It.IsAny<Guid>(), false, -25, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);

        // HU-25: an incorrect-answer record still gets recorded.
        _statsRepoMock.Verify(
            r => r.AddAsync(
                It.Is<StageCompletionRecord>(rec =>
                    rec.StageType == "Trivia"
                    && rec.WasCorrect == false),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── HU-26: command audit ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CorrectAnswer_WritesAuditEventWithTeamActorAndOutcomeSuccess()
    {
        var session = CreateSessionWithStatus(SessionStatus.InProgress);
        var stageId = Guid.NewGuid();
        var correctOptionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var correctOption = new TriviaOptionInfo(correctOptionId, "Correct", true);
        var stageInfo = new StageWithOptionsInfo(stageId, "Stage 1", "Trivia", 1, 50, "Q?",
            new[] { correctOption });

        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(session);
        _stageClientMock.Setup(s => s.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(stageInfo);
        _stageClientMock.Setup(s => s.GetStagesByMissionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new[] { new StageInfo(stageId, 1) });
        _teamClientMock.Setup(t => t.RecordEvidenceOutcomeAsync(teamId, true, 50, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new StageTransitionResult(NewScore: 200, ElapsedSeconds: 10));
        _teamClientMock.Setup(t => t.GetTeamByIdAsync(teamId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new TeamInfoItem(teamId, "Alfa", 1));

        SessionAuditIntegrationEvent? captured = null;
        _busMock
            .Setup(b => b.PublishAsync(It.IsAny<SessionAuditIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<SessionAuditIntegrationEvent, CancellationToken>((e, _) => captured = e);

        var result = await _handler.Handle(
            new SubmitTriviaAnswerCommand(Guid.NewGuid(), teamId, stageId, correctOptionId),
            default);

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.ActorName.Should().Be("Equipo Alfa");
        captured.CommandType.Should().Be(nameof(SubmitTriviaAnswerCommand));
        captured.Outcome.Should().Be(SessionEvent.OutcomeSuccess);
    }

    [Fact]
    public async Task Handle_IncorrectAnswer_WritesAuditEventWithOutcomeFailure()
    {
        var session = CreateSessionWithStatus(SessionStatus.InProgress);
        var stageId = Guid.NewGuid();
        var wrongOptionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var wrongOption = new TriviaOptionInfo(wrongOptionId, "Wrong", false);
        var stageInfo = new StageWithOptionsInfo(stageId, "Stage 1", "Trivia", 1, 50, "Q?",
            new[] { wrongOption });

        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(session);
        _stageClientMock.Setup(s => s.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(stageInfo);
        _stageClientMock.Setup(s => s.GetStagesByMissionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new[] { new StageInfo(stageId, 1) });
        // Medium strategy: wrong answer → -25
        _teamClientMock.Setup(t => t.RecordEvidenceOutcomeAsync(teamId, false, -25, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new StageTransitionResult(NewScore: 25, ElapsedSeconds: 12));
        _teamClientMock.Setup(t => t.GetTeamByIdAsync(teamId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new TeamInfoItem(teamId, "Beta", 1));

        SessionAuditIntegrationEvent? captured = null;
        _busMock
            .Setup(b => b.PublishAsync(It.IsAny<SessionAuditIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<SessionAuditIntegrationEvent, CancellationToken>((e, _) => captured = e);

        var result = await _handler.Handle(
            new SubmitTriviaAnswerCommand(Guid.NewGuid(), teamId, stageId, wrongOptionId),
            default);

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.Outcome.Should().Be(SessionEvent.OutcomeFailure);
        captured.ActorName.Should().Be("Equipo Beta");
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
