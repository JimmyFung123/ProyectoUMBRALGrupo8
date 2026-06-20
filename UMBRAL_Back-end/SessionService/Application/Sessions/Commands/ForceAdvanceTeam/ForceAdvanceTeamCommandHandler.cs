namespace SessionService.Application.Sessions.Commands.ForceAdvanceTeam;

using MediatR;
using SessionService.Application;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using SessionService.Domain.Statistics;
using UMBRAL.Contracts.Events;

public class ForceAdvanceTeamCommandHandler : IRequestHandler<ForceAdvanceTeamCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamClient;
    private readonly IStageServiceClient _stageClient;
    private readonly IIntegrationEventBus _bus;
    private readonly IStageCompletionRecordRepository _statsRepository;
    private readonly ISessionNotifier _notifier;

    public ForceAdvanceTeamCommandHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient,
        IStageServiceClient stageClient,
        IIntegrationEventBus bus,
        IStageCompletionRecordRepository statsRepository,
        ISessionNotifier notifier)
    {
        _sessionRepository = sessionRepository;
        _teamClient = teamClient;
        _stageClient = stageClient;
        _bus = bus;
        _statsRepository = statsRepository;
        _notifier = notifier;
    }

    public async Task<Result<bool>> Handle(ForceAdvanceTeamCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate session exists and is InProgress
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<bool>(SessionErrors.NotFound);
        if (session.Status != SessionStatus.InProgress)
            return Result.Failure<bool>(SessionErrors.CannotForceAdvance);

        // 2. Get the target team's current progress
        var teams = await _teamClient.GetTeamProgressAsync(request.SessionId, cancellationToken);
        var team = teams.FirstOrDefault(t => t.Id == request.TeamId);
        if (team is null)
            return Result.Failure<bool>(SessionErrors.TeamNotFound);

        // 3. Get stages to validate the team is not already on the last one
        var stages = await _stageClient.GetStagesByMissionAsync(session.MissionId, cancellationToken);
        if (stages.Count == 0)
            return Result.Failure<bool>(SessionErrors.CannotForceAdvance);

        var maxOrder = stages.Max(s => s.Order);
        var nextOrder = team.CurrentStageOrder + 1;

        if (nextOrder > maxOrder)
            return Result.Failure<bool>(SessionErrors.TeamAlreadyOnLastStage);

        // Capture the type of the stage being skipped — needed for the analytics row.
        var abandonedStage = stages.FirstOrDefault(s => s.Order == team.CurrentStageOrder);
        var abandonedStageType = abandonedStage?.Type ?? "Trivia";

        // 4. Force the advance in TeamService
        var outcome = await _teamClient.ForceAdvanceTeamAsync(request.TeamId, nextOrder, cancellationToken);
        if (outcome is null)
            return Result.Failure<bool>(SessionErrors.CannotForceAdvance);

        // 5. HU-25: append the analytics fact row. WasCorrect is null because
        // no answer was actually given — the operator overrode the flow.
        var statsRecord = StageCompletionRecord.ForForceAdvance(
            sessionId: request.SessionId,
            missionId: session.MissionId,
            teamId: request.TeamId,
            stageOrder: team.CurrentStageOrder,
            stageType: abandonedStageType,
            elapsedSeconds: outcome.ElapsedSeconds);
        await _statsRepository.AddAsync(statsRecord, cancellationToken);
        await _statsRepository.SaveChangesAsync(cancellationToken);

        // 6. Audit log (HU-22 / HU-26)
        await _bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                request.SessionId,
                $"El operador forzó el avance del equipo '{team.Name}' de la etapa {team.CurrentStageOrder} a la etapa {nextOrder}.",
                ActorName: request.OperatorName,
                CommandType: nameof(ForceAdvanceTeamCommand),
                Outcome: SessionEvent.OutcomeSuccess,
                DateTime.UtcNow),
            cancellationToken);

        // 7. Broadcast to refresh dashboard
        await _notifier.NotifyStateChangedAsync(request.SessionId, ct: cancellationToken);

        return Result.Success(true);
    }
}
