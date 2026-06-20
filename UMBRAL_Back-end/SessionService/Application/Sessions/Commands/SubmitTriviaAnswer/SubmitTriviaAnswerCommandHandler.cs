namespace SessionService.Application.Sessions.Commands.SubmitTriviaAnswer;

using SessionService.Application;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Commands.Evidence;
using SessionService.Application.Sessions.Scoring;
using SessionService.Domain.Common;
using SessionService.Domain.MissionLookup;
using SessionService.Domain.Sessions;
using SessionService.Domain.Statistics;
using UMBRAL.Contracts.Events;

/// <summary>
/// Concrete Template Method for trivia-answer evidence (HU-18).
/// Inherits the shared session/stage validation, navigation, TeamService call,
/// and SignalR broadcast from <see cref="EvidenceHandlerBase{TCommand,TResultDto}"/>.
/// Implements: option validation, Strategy-based scoring, trivia analytics, trivia audit.
/// </summary>
public class SubmitTriviaAnswerCommandHandler
    : EvidenceHandlerBase<SubmitTriviaAnswerCommand, TriviaAnswerResultDto>
{
    private readonly IMissionLookupRepository _missionLookupRepository;

    public SubmitTriviaAnswerCommandHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient,
        IStageServiceClient stageClient,
        IStageCompletionRecordRepository statsRepository,
        IIntegrationEventBus bus,
        ISessionNotifier notifier,
        IMissionLookupRepository missionLookupRepository)
        : base(sessionRepository, teamClient, stageClient, statsRepository, bus, notifier)
    {
        _missionLookupRepository = missionLookupRepository;
    }

    // ── Command field accessors ───────────────────────────────────────────────

    protected override Guid GetSessionId(SubmitTriviaAnswerCommand c) => c.SessionId;
    protected override Guid GetTeamId(SubmitTriviaAnswerCommand c)    => c.TeamId;
    protected override Guid GetStageId(SubmitTriviaAnswerCommand c)   => c.StageId;
    protected override string GetStageType()                           => "Trivia";
    protected override Error GetRecordingError()                       => SessionErrors.CannotAnswerTrivia;

    // ── Hook: validate trivia option + apply scoring strategy ────────────────

    protected override async Task<Result<EvidenceOutcome>> ProcessEvidenceAsync(
        SubmitTriviaAnswerCommand command,
        Session session,
        StageWithOptionsInfo stage,
        CancellationToken ct)
    {
        var option = stage.Options.FirstOrDefault(o => o.Id == command.OptionId);
        if (option is null)
            return Result.Failure<EvidenceOutcome>(SessionErrors.OptionNotFound);

        bool isCorrect = option.IsCorrect;

        var missionLookup = await _missionLookupRepository.GetByIdAsync(session.MissionId, ct);
        var strategy  = ScoringStrategyFactory.Create(missionLookup?.Difficulty ?? "Medium");
        int scoreChange = strategy.Calculate(stage.BaseScore, isCorrect);

        // Trivia always advances the stage regardless of correctness
        return Result.Success(new EvidenceOutcome(
            IsCorrect: isCorrect,
            ScoreChange: scoreChange,
            ShouldAdvance: true));
    }

    // ── Hook: trivia never has a wrong-answer early return ───────────────────

    protected override TriviaAnswerResultDto BuildEarlyResult(
        EvidenceOutcome outcome, SubmitTriviaAnswerCommand command)
        => throw new InvalidOperationException(
            "Trivia evidence always advances the stage; BuildEarlyResult should never be called.");

    // ── Hook: analytics ──────────────────────────────────────────────────────

    protected override async Task RecordAnalyticsAsync(
        SubmitTriviaAnswerCommand command, Session session,
        StageWithOptionsInfo stage, bool isCorrect, StageTransitionResult transition, CancellationToken ct)
    {
        var record = StageCompletionRecord.ForTriviaAnswer(
            sessionId:      command.SessionId,
            missionId:      session.MissionId,
            teamId:         command.TeamId,
            stageOrder:     stage.Order,
            elapsedSeconds: transition.ElapsedSeconds,
            wasCorrect:     isCorrect);
        await StatsRepository.AddAsync(record, ct);
        await StatsRepository.SaveChangesAsync(ct);
    }

    // ── Hook: audit log ──────────────────────────────────────────────────────

    protected override async Task WriteAuditAsync(
        SubmitTriviaAnswerCommand command, Session session,
        StageWithOptionsInfo stage, int scoreChange,
        StageTransitionResult transition, CancellationToken ct)
    {
        var teamInfo  = await TeamClient.GetTeamByIdAsync(command.TeamId, ct);
        var teamName  = teamInfo?.Name ?? "Equipo";
        bool isCorrect = scoreChange > 0;

        var message = isCorrect
            ? $"El equipo '{teamName}' respondió correctamente la etapa {stage.Order} y sumó {scoreChange} pts. Nuevo puntaje: {transition.NewScore}."
            : $"El equipo '{teamName}' respondió incorrectamente la etapa {stage.Order} ({scoreChange} pts). Nuevo puntaje: {transition.NewScore}.";

        await Bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                command.SessionId, message,
                ActorName:   $"Equipo {teamName}",
                CommandType: nameof(SubmitTriviaAnswerCommand),
                Outcome:     isCorrect ? SessionEvent.OutcomeSuccess : SessionEvent.OutcomeFailure,
                DateTime.UtcNow),
            ct);
    }

    // ── Hook: build result DTO ───────────────────────────────────────────────

    protected override TriviaAnswerResultDto BuildSuccessResult(
        bool isCorrect, StageTransitionResult transition, StageNavigation nav)
        => new(isCorrect, transition.NewScore, nav.NextStageOrder, nav.IsLastStage);
}
