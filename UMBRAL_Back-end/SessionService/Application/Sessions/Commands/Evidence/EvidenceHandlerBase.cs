namespace SessionService.Application.Sessions.Commands.Evidence;

using MediatR;
using SessionService.Application;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Validation;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using SessionService.Domain.Statistics;
using UMBRAL.Contracts.Events;

/// <summary>
/// Template Method pattern — defines the invariant algorithm for processing
/// evidence (trivia answers and QR scans) during a live session.
///
/// Shared steps handled here:
///   1. Validate session (exists + InProgress)
///   2. Fetch stage with options
///   3. HOOK: ProcessEvidenceAsync  ← varies per evidence type
///   4. Early return if evidence was rejected (e.g. wrong QR scan)
///   5. Compute next-stage navigation
///   6. Record result in TeamService (score + stage advance)
///   7. HOOK: RecordAnalyticsAsync  ← varies per evidence type
///   8. HOOK: WriteAuditAsync       ← varies per evidence type
///   9. Broadcast via SignalR
///  10. HOOK: BuildSuccessResult    ← maps shared data to concrete DTO
/// </summary>
public abstract class EvidenceHandlerBase<TCommand, TResultDto>
    : IRequestHandler<TCommand, Result<TResultDto>>
    where TCommand : IRequest<Result<TResultDto>>
{
    protected readonly ISessionRepository SessionRepository;
    protected readonly ITeamServiceClient TeamClient;
    protected readonly IStageServiceClient StageClient;
    protected readonly IStageCompletionRecordRepository StatsRepository;
    protected readonly IIntegrationEventBus Bus;

    protected EvidenceHandlerBase(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient,
        IStageServiceClient stageClient,
        IStageCompletionRecordRepository statsRepository,
        IIntegrationEventBus bus)
    {
        SessionRepository = sessionRepository;
        TeamClient        = teamClient;
        StageClient       = stageClient;
        StatsRepository   = statsRepository;
        Bus               = bus;
    }

    // ── Validation chain (Chain of Responsibility) ────────────────────────────

    /// <summary>
    /// Builds the pre-processing validation chain: session exists →
    /// session is in progress → stage exists.
    /// Each link either rejects with a domain error or delegates to the next.
    /// </summary>
    private static EvidenceValidatorBase BuildValidationChain()
    {
        var sessionExists    = new SessionExistsValidator();
        var sessionActive    = new SessionInProgressValidator();
        var stageExists      = new StageExistsValidator();

        sessionExists.SetNext(sessionActive).SetNext(stageExists);
        return sessionExists;
    }

    // ── Template Method ───────────────────────────────────────────────────────

    public async Task<Result<TResultDto>> Handle(TCommand command, CancellationToken ct)
    {
        // Steps 1-2: fetch session + stage, then validate via CoR chain
        var session = await SessionRepository.GetByIdAsync(GetSessionId(command), ct);
        var stage   = await StageClient.GetStageWithOptionsAsync(GetStageId(command), ct);

        var context        = new EvidenceValidationContext(session, stage);
        var validationResult = BuildValidationChain().Validate(context);
        if (validationResult.IsFailure) return Fail(validationResult.Error);

        // Step 3: HOOK — process evidence (validate option / validate QR)
        var evidenceResult = await ProcessEvidenceAsync(command, session, stage, ct);
        if (evidenceResult.IsFailure)  return Fail(evidenceResult.Error);

        var evidence = evidenceResult.Value;

        // Step 4: early return when evidence was rejected without stage advance
        // (e.g. wrong QR scan — team stays on the same stage, no points changed)
        if (!evidence.ShouldAdvance)
            return Result.Success(BuildEarlyResult(evidence, command));

        // Step 5: compute navigation (next stage order + is-last flag)
        var nav = await ComputeNavigationAsync(session.MissionId, GetStageId(command), ct);
        if (nav is null)               return Fail(SessionErrors.StageNotFound);

        // Step 6: record in TeamService — updates score + advances stage order
        var transition = await TeamClient.RecordEvidenceOutcomeAsync(
            GetTeamId(command), evidence.IsCorrect, evidence.ScoreChange, nav.NextStageOrder, ct);
        if (transition is null)        return Fail(GetRecordingError());

        // Step 7: HOOK — append analytics fact row (varies by evidence type)
        await RecordAnalyticsAsync(command, session, stage, evidence.IsCorrect, transition, ct);

        // Step 8: HOOK — write command audit log (varies by evidence type)
        await WriteAuditAsync(command, session, stage, evidence.ScoreChange, transition, ct);

        // Step 9: broadcast to operators (dashboard refresh) and participants (HU-28)
        await BroadcastAsync(command, session, stage, evidence, transition, nav, ct);

        // Step 10: HOOK — map shared data to the concrete result DTO
        return Result.Success(BuildSuccessResult(evidence.IsCorrect, transition, nav));
    }

    // ── Abstract hooks ────────────────────────────────────────────────────────

    /// <summary>
    /// Validates the evidence specific to the stage type.
    /// May write an early audit event and return ShouldAdvance=false for rejected evidence.
    /// </summary>
    protected abstract Task<Result<EvidenceOutcome>> ProcessEvidenceAsync(
        TCommand command, Session session, StageWithOptionsInfo stage, CancellationToken ct);

    /// <summary>
    /// Builds the result DTO when the evidence was rejected (ShouldAdvance=false).
    /// </summary>
    protected abstract TResultDto BuildEarlyResult(EvidenceOutcome outcome, TCommand command);

    /// <summary>Appends the analytics fact row to the statistics store.</summary>
    protected abstract Task RecordAnalyticsAsync(
        TCommand command, Session session, StageWithOptionsInfo stage,
        bool isCorrect, StageTransitionResult transition, CancellationToken ct);

    /// <summary>Writes the command audit log entry (HU-22 / HU-26).</summary>
    protected abstract Task WriteAuditAsync(
        TCommand command, Session session, StageWithOptionsInfo stage,
        int scoreChange, StageTransitionResult transition, CancellationToken ct);

    /// <summary>Returns the error to surface when TeamService fails to record the result.</summary>
    protected abstract Error GetRecordingError();

    /// <summary>Maps the shared result data to the concrete DTO.</summary>
    protected abstract TResultDto BuildSuccessResult(
        bool isCorrect, StageTransitionResult transition, StageNavigation nav);

    /// <summary>Returns the stage type label used in the SignalR broadcast payload.</summary>
    protected abstract string GetStageType();

    // ── Command field accessors ───────────────────────────────────────────────

    protected abstract Guid GetSessionId(TCommand command);
    protected abstract Guid GetTeamId(TCommand command);
    protected abstract Guid GetStageId(TCommand command);

    // ── Concrete shared helpers ───────────────────────────────────────────────

    private Result<TResultDto> Fail(Error error) => Result.Failure<TResultDto>(error);

    private async Task<StageNavigation?> ComputeNavigationAsync(
        Guid missionId, Guid stageId, CancellationToken ct)
    {
        var allStages = await StageClient.GetStagesByMissionAsync(missionId, ct);
        var sorted = allStages.OrderBy(s => s.Order).ToList();
        if (sorted.Count == 0) return null;

        var index = sorted.FindIndex(s => s.Id == stageId);
        if (index < 0) return null;

        bool isLast = index == sorted.Count - 1;
        int next    = isLast ? sorted.Max(s => s.Order) + 1 : sorted[index + 1].Order;
        return new StageNavigation(next, isLast);
    }

    private Task BroadcastAsync(
        TCommand command, Session session, StageWithOptionsInfo stage,
        EvidenceOutcome evidence, StageTransitionResult transition,
        StageNavigation nav, CancellationToken ct)
        => Bus.PublishAsync(
            new StageCompletedIntegrationEvent(
                SessionId:      GetSessionId(command),
                TeamId:         GetTeamId(command),
                StageOrder:     stage.Order,
                StageType:      GetStageType(),
                WasCorrect:     evidence.IsCorrect,
                PointsEarned:   evidence.ScoreChange,
                NewScore:       transition.NewScore,
                NextStageOrder: nav.NextStageOrder,
                IsLastStage:    nav.IsLastStage),
            ct);
}
