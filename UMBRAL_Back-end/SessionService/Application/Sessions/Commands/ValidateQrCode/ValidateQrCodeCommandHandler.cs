namespace SessionService.Application.Sessions.Commands.ValidateQrCode;

using SessionService.Application;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Commands.Evidence;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using SessionService.Domain.Statistics;
using UMBRAL.Contracts.Events;

/// <summary>
/// Concrete Template Method for QR-code evidence (HU-19).
/// Inherits the shared session/stage validation, navigation, TeamService call,
/// and SignalR broadcast from <see cref="EvidenceHandlerBase{TCommand,TResultDto}"/>.
/// Implements: TreasureHunt type check, QR comparison, treasure-hunt analytics, QR audit.
/// </summary>
public class ValidateQrCodeCommandHandler
    : EvidenceHandlerBase<ValidateQrCodeCommand, QrValidationResultDto>
{
    public ValidateQrCodeCommandHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient,
        IStageServiceClient stageClient,
        IStageCompletionRecordRepository statsRepository,
        IIntegrationEventBus bus,
        ISessionNotifier notifier)
        : base(sessionRepository, teamClient, stageClient, statsRepository, bus, notifier)
    { }

    // ── Command field accessors ───────────────────────────────────────────────

    protected override Guid GetSessionId(ValidateQrCodeCommand c) => c.SessionId;
    protected override Guid GetTeamId(ValidateQrCodeCommand c)    => c.TeamId;
    protected override Guid GetStageId(ValidateQrCodeCommand c)   => c.StageId;
    protected override string GetStageType()                        => "TreasureHunt";
    protected override Error GetRecordingError()                    => SessionErrors.CannotValidateQr;

    // ── Hook: validate TreasureHunt stage type + compare scanned QR ──────────

    protected override async Task<Result<EvidenceOutcome>> ProcessEvidenceAsync(
        ValidateQrCodeCommand command,
        Session session,
        StageWithOptionsInfo stage,
        CancellationToken ct)
    {
        if (!string.Equals(stage.Type, "TreasureHunt", StringComparison.OrdinalIgnoreCase))
            return Result.Failure<EvidenceOutcome>(SessionErrors.StageIsNotTreasureHunt);

        if (string.IsNullOrWhiteSpace(stage.QrCode))
            return Result.Failure<EvidenceOutcome>(SessionErrors.StageMissingQrCode);

        bool isCorrect = string.Equals(
            stage.QrCode.Trim(),
            command.ScannedCode?.Trim(),
            StringComparison.OrdinalIgnoreCase);

        if (!isCorrect)
        {
            // Wrong scan — write early audit log and return without advancing stage
            var team = await TeamClient.GetTeamByIdAsync(command.TeamId, ct);
            if (team is null)
                return Result.Failure<EvidenceOutcome>(SessionErrors.TeamNotFound);

            await Bus.PublishAsync(
                new SessionAuditIntegrationEvent(
                    command.SessionId,
                    $"El equipo '{team.Name}' escaneó un QR incorrecto en la etapa {stage.Order}.",
                    ActorName:   $"Equipo {team.Name}",
                    CommandType: nameof(ValidateQrCodeCommand),
                    Outcome:     SessionEvent.OutcomeFailure,
                    DateTime.UtcNow),
                ct);

            return Result.Success(new EvidenceOutcome(
                IsCorrect:             false,
                ScoreChange:           0,
                ShouldAdvance:         false,
                TeamCurrentStageOrder: team.CurrentStageOrder));
        }

        // Correct scan — base score (TreasureHunt doesn't use difficulty multiplier)
        return Result.Success(new EvidenceOutcome(
            IsCorrect:     true,
            ScoreChange:   stage.BaseScore,
            ShouldAdvance: true));
    }

    // ── Hook: wrong scan early return ─────────────────────────────────────────

    protected override QrValidationResultDto BuildEarlyResult(
        EvidenceOutcome outcome, ValidateQrCodeCommand command)
        => new(IsCorrect: false, NewScore: 0,
               NextStageOrder: outcome.TeamCurrentStageOrder, IsLastStage: false);

    // ── Hook: analytics ──────────────────────────────────────────────────────

    protected override async Task RecordAnalyticsAsync(
        ValidateQrCodeCommand command, Session session,
        StageWithOptionsInfo stage, bool isCorrect, StageTransitionResult transition, CancellationToken ct)
    {
        var record = StageCompletionRecord.ForTreasureHuntScan(
            sessionId:      command.SessionId,
            missionId:      session.MissionId,
            teamId:         command.TeamId,
            stageOrder:     stage.Order,
            elapsedSeconds: transition.ElapsedSeconds,
            wasCorrect:     true);
        await StatsRepository.AddAsync(record, ct);
        await StatsRepository.SaveChangesAsync(ct);
    }

    // ── Hook: audit log ──────────────────────────────────────────────────────

    protected override async Task WriteAuditAsync(
        ValidateQrCodeCommand command, Session session,
        StageWithOptionsInfo stage, int scoreChange,
        StageTransitionResult transition, CancellationToken ct)
    {
        var teamInfo = await TeamClient.GetTeamByIdAsync(command.TeamId, ct);
        var teamName = teamInfo?.Name ?? "Equipo";

        await Bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                command.SessionId,
                $"El equipo '{teamName}' resolvió la etapa {stage.Order} escaneando el código QR.",
                ActorName:   $"Equipo {teamName}",
                CommandType: nameof(ValidateQrCodeCommand),
                Outcome:     SessionEvent.OutcomeSuccess,
                DateTime.UtcNow),
            ct);
    }

    // ── Hook: build result DTO ───────────────────────────────────────────────

    protected override QrValidationResultDto BuildSuccessResult(
        bool isCorrect, StageTransitionResult transition, StageNavigation nav)
        => new(IsCorrect: true, NewScore: transition.NewScore,
               NextStageOrder: nav.NextStageOrder, IsLastStage: nav.IsLastStage);
}
