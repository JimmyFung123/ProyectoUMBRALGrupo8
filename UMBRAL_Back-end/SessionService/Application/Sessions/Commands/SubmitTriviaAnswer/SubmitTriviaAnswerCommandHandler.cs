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
///
/// Wrong-answer blocking logic:
///   - AutoReleaseMaxAttempts == null or 0 → always advance (legacy behaviour).
///   - Wrong answer + attempts remaining → record wrong attempt in TeamService,
///     publish TriviaWrongAnswerIntegrationEvent, return ShouldAdvance=false.
///   - Wrong answer + no attempts left → advance with 0 additional score change
///     (penalty already applied by the last RecordWrongAttemptAsync call).
///   - Correct answer → advance normally.
/// </summary>
public class SubmitTriviaAnswerCommandHandler
    : EvidenceHandlerBase<SubmitTriviaAnswerCommand, TriviaAnswerResultDto>
{
    private readonly IMissionLookupRepository _missionLookupRepository;
    private readonly IClueServiceClient _clueServiceClient;

    public SubmitTriviaAnswerCommandHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient,
        IStageServiceClient stageClient,
        IStageCompletionRecordRepository statsRepository,
        IIntegrationEventBus bus,
        IMissionLookupRepository missionLookupRepository,
        IClueServiceClient clueServiceClient)
        : base(sessionRepository, teamClient, stageClient, statsRepository, bus)
    {
        _missionLookupRepository = missionLookupRepository;
        _clueServiceClient = clueServiceClient;
    }

    // ── Command field accessors ───────────────────────────────────────────────

    protected override Guid GetSessionId(SubmitTriviaAnswerCommand c) => c.SessionId;
    protected override Guid GetTeamId(SubmitTriviaAnswerCommand c)    => c.TeamId;
    protected override Guid GetStageId(SubmitTriviaAnswerCommand c)   => c.StageId;
    protected override string GetStageType()                           => "Trivia";
    protected override Error GetRecordingError()                       => SessionErrors.CannotAnswerTrivia;

    // ── Hook: validate trivia option + attempt blocking logic ─────────────────

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
        var strategy      = ScoringStrategyFactory.Create(missionLookup?.Difficulty ?? "Medium");
        int scoreChange   = strategy.Calculate(stage.BaseScore, isCorrect);

        int maxAttempts = stage.AutoReleaseMaxAttempts ?? 0;

        // No attempt limit or correct answer → always advance.
        if (isCorrect || maxAttempts == 0)
            return Result.Success(new EvidenceOutcome(isCorrect, scoreChange, ShouldAdvance: true));

        // Wrong answer with attempt limit: record in TeamService.
        var wrongResult = await TeamClient.RecordWrongAttemptAsync(
            command.TeamId, command.OptionId, scoreChange, ct);

        if (wrongResult is null)
            return Result.Failure<EvidenceOutcome>(SessionErrors.CannotAnswerTrivia);

        if (wrongResult.NewWrongCount < maxAttempts)
        {
            // Attempts remaining → block option, do NOT advance.
            await Bus.PublishAsync(
                new TriviaWrongAnswerIntegrationEvent(
                    SessionId:       command.SessionId,
                    TeamId:          command.TeamId,
                    StageOrder:      stage.Order,
                    BlockedOptionId: command.OptionId,
                    AttemptsUsed:    wrongResult.NewWrongCount,
                    MaxAttempts:     maxAttempts,
                    ScoreChange:     scoreChange,
                    NewScore:        wrongResult.NewScore,
                    ParticipantName: command.ParticipantName),
                ct);

            // Pistas por reintento (regla "Intentos fallidos"): cada fallo con
            // intentos restantes libera la siguiente pista de la etapa para ayudar
            // al equipo. Paralelo a la auto-liberación por tiempo (HU-14).
            await ReleaseNextClueOnWrongAttemptAsync(command, ct);

            return Result.Success(new EvidenceOutcome(
                IsCorrect:             false,
                ScoreChange:           0,
                ShouldAdvance:         false,
                TeamCurrentStageOrder: stage.Order,
                WrongAttempt: new WrongAttemptInfo(
                    command.OptionId,
                    wrongResult.NewWrongCount,
                    maxAttempts,
                    wrongResult.NewScore)));
        }

        // Out of attempts → advance; score penalty already applied.
        return Result.Success(new EvidenceOutcome(
            IsCorrect:     false,
            ScoreChange:   0,
            ShouldAdvance: true));
    }

    /// <summary>
    /// "Pistas por reintento": libera automáticamente la siguiente pista de la
    /// etapa tras una respuesta incorrecta con intentos restantes. No hace nada
    /// si la etapa no tiene pistas o si ya se liberaron todas. El participante la
    /// recibe vía ClueReleasedIntegrationEvent → ClueReleasedConsumer (SignalR).
    /// </summary>
    private async Task ReleaseNextClueOnWrongAttemptAsync(
        SubmitTriviaAnswerCommand command, CancellationToken ct)
    {
        var clues = (await _clueServiceClient.GetCluesByStageAsync(command.StageId, ct))
            .OrderBy(c => c.Order)
            .ToList();
        if (clues.Count == 0) return;

        var clueNumber = await TeamClient.ReleaseClueAsync(
            command.TeamId, clues.Count, ct, isAutomatic: true);
        if (clueNumber <= 0) return; // ya se habían liberado todas las pistas

        var released = clues.ElementAtOrDefault(clueNumber - 1);
        if (released is null) return;

        await Bus.PublishAsync(
            new ClueReleasedIntegrationEvent(
                command.SessionId, command.TeamId,
                released.Content, released.Latitude, released.Longitude, released.RadiusMeters,
                clueNumber, IsAutomatic: true),
            ct);
    }

    // ── Hook: blocked result (no stage advance) ───────────────────────────────

    protected override TriviaAnswerResultDto BuildEarlyResult(
        EvidenceOutcome outcome, SubmitTriviaAnswerCommand command)
    {
        var wa = outcome.WrongAttempt!;
        return new TriviaAnswerResultDto(
            IsCorrect:       false,
            NewScore:        wa.NewScore,
            NextStageOrder:  outcome.TeamCurrentStageOrder,
            IsLastStage:     false,
            ShouldAdvance:   false,
            BlockedOptionId: wa.BlockedOptionId,
            AttemptsUsed:    wa.AttemptsUsed,
            MaxAttempts:     wa.MaxAttempts);
    }

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
