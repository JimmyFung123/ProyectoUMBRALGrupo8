namespace SessionService.Application.Sessions.Commands.ValidateQrCode;

using MediatR;
using Microsoft.AspNetCore.SignalR;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using SessionService.Domain.Statistics;
using SessionService.Infrastructure.Hubs;

public class ValidateQrCodeCommandHandler : IRequestHandler<ValidateQrCodeCommand, Result<QrValidationResultDto>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamClient;
    private readonly IStageServiceClient _stageClient;
    private readonly ISessionEventRepository _eventRepository;
    private readonly IStageCompletionRecordRepository _statsRepository;
    private readonly IHubContext<SessionHub> _hub;

    public ValidateQrCodeCommandHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient,
        IStageServiceClient stageClient,
        ISessionEventRepository eventRepository,
        IStageCompletionRecordRepository statsRepository,
        IHubContext<SessionHub> hub)
    {
        _sessionRepository = sessionRepository;
        _teamClient = teamClient;
        _stageClient = stageClient;
        _eventRepository = eventRepository;
        _statsRepository = statsRepository;
        _hub = hub;
    }

    public async Task<Result<QrValidationResultDto>> Handle(ValidateQrCodeCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate session exists and accepts evidence (RB-03)
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<QrValidationResultDto>(SessionErrors.NotFound);

        if (session.Status != SessionStatus.InProgress)
            return Result.Failure<QrValidationResultDto>(SessionErrors.NotInProgress);

        // 2. Fetch the stage (QrCode is internal — never sent to participant)
        var stage = await _stageClient.GetStageWithOptionsAsync(request.StageId, cancellationToken);
        if (stage is null)
            return Result.Failure<QrValidationResultDto>(SessionErrors.StageNotFound);

        if (!string.Equals(stage.Type, "TreasureHunt", StringComparison.OrdinalIgnoreCase))
            return Result.Failure<QrValidationResultDto>(SessionErrors.StageIsNotTreasureHunt);

        if (string.IsNullOrWhiteSpace(stage.QrCode))
            return Result.Failure<QrValidationResultDto>(SessionErrors.StageMissingQrCode);

        // 3. Validate the scanned code against the stage's QR
        bool isCorrect = string.Equals(
            stage.QrCode.Trim(),
            request.ScannedCode?.Trim(),
            StringComparison.OrdinalIgnoreCase);

        // 4. If the scan is wrong, the team stays on this stage and does NOT earn points (HU-19, criterion 3)
        if (!isCorrect)
        {
            var team = await _teamClient.GetTeamByIdAsync(request.TeamId, cancellationToken);
            if (team is null)
                return Result.Failure<QrValidationResultDto>(SessionErrors.TeamNotFound);

            // Audit failed scan attempt (HU-22 / HU-26)
            var failedEvent = SessionEvent.Create(
                request.SessionId,
                $"El equipo '{team.Name}' escaneó un QR incorrecto en la etapa {stage.Order}.",
                actorName: $"Equipo {team.Name}",
                commandType: nameof(ValidateQrCodeCommand),
                outcome: SessionEvent.OutcomeFailure);
            await _eventRepository.AddAsync(failedEvent, cancellationToken);
            await _eventRepository.SaveChangesAsync(cancellationToken);

            return Result.Success(new QrValidationResultDto(
                IsCorrect: false,
                NewScore: 0,
                NextStageOrder: team.CurrentStageOrder,
                IsLastStage: false));
        }

        // 5. Correct scan — compute next stage order
        var allStages = await _stageClient.GetStagesByMissionAsync(session.MissionId, cancellationToken);
        var sortedStages = allStages.OrderBy(s => s.Order).ToList();
        if (sortedStages.Count == 0)
            return Result.Failure<QrValidationResultDto>(SessionErrors.StageNotFound);

        var maxOrder = sortedStages.Max(s => s.Order);
        var currentStageIndex = sortedStages.FindIndex(s => s.Id == request.StageId);
        if (currentStageIndex < 0)
            return Result.Failure<QrValidationResultDto>(SessionErrors.StageNotFound);

        bool isLastStage = currentStageIndex == sortedStages.Count - 1;
        int nextStageOrder = isLastStage
            ? maxOrder + 1
            : sortedStages[currentStageIndex + 1].Order;

        // 6. Record the success in TeamService (reuses AnswerTriviaAsync: adds points + advances stage)
        var outcome = await _teamClient.AnswerTriviaAsync(
            request.TeamId, isCorrect: true, stage.BaseScore, nextStageOrder, cancellationToken);

        if (outcome is null)
            return Result.Failure<QrValidationResultDto>(SessionErrors.CannotValidateQr);

        // 7. HU-25: append the analytics fact row for the treasure hunt stage.
        var statsRecord = StageCompletionRecord.ForTreasureHuntScan(
            sessionId: request.SessionId,
            missionId: session.MissionId,
            teamId: request.TeamId,
            stageOrder: stage.Order,
            elapsedSeconds: outcome.ElapsedSeconds,
            wasCorrect: true);
        await _statsRepository.AddAsync(statsRecord, cancellationToken);
        await _statsRepository.SaveChangesAsync(cancellationToken);

        // 8. Audit successful scan
        var teamInfo = await _teamClient.GetTeamByIdAsync(request.TeamId, cancellationToken);
        var teamName = teamInfo?.Name ?? "Equipo";
        var successEvent = SessionEvent.Create(
            request.SessionId,
            $"El equipo '{teamName}' resolvió la etapa {stage.Order} escaneando el código QR.",
            actorName: $"Equipo {teamName}",
            commandType: nameof(ValidateQrCodeCommand),
            outcome: SessionEvent.OutcomeSuccess);
        await _eventRepository.AddAsync(successEvent, cancellationToken);
        await _eventRepository.SaveChangesAsync(cancellationToken);

        // 9. Broadcast dashboard refresh
        await _hub.Clients
            .Group(request.SessionId.ToString())
            .SendAsync("SessionStateChanged", cancellationToken);

        return Result.Success(new QrValidationResultDto(
            IsCorrect: true,
            NewScore: outcome.NewScore,
            NextStageOrder: nextStageOrder,
            IsLastStage: isLastStage));
    }
}
