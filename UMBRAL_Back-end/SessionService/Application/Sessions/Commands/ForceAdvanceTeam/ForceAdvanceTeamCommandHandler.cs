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

    public ForceAdvanceTeamCommandHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient,
        IStageServiceClient stageClient,
        IIntegrationEventBus bus,
        IStageCompletionRecordRepository statsRepository)
    {
        _sessionRepository = sessionRepository;
        _teamClient = teamClient;
        _stageClient = stageClient;
        _bus = bus;
        _statsRepository = statsRepository;
    }

    public async Task<Result<bool>> Handle(ForceAdvanceTeamCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<bool>(SessionErrors.NotFound);
        if (session.Status != SessionStatus.InProgress)
            return Result.Failure<bool>(SessionErrors.CannotForceAdvance);

        var teams = await _teamClient.GetTeamProgressAsync(request.SessionId, cancellationToken);
        var team = teams.FirstOrDefault(t => t.Id == request.TeamId);
        if (team is null)
            return Result.Failure<bool>(SessionErrors.TeamNotFound);

        var stages = await _stageClient.GetStagesByMissionAsync(session.MissionId, cancellationToken);
        if (stages.Count == 0)
            return Result.Failure<bool>(SessionErrors.CannotForceAdvance);

        var maxOrder = stages.Max(s => s.Order);
        var nextOrder = team.CurrentStageOrder + 1;

        if (nextOrder > maxOrder)
            return Result.Failure<bool>(SessionErrors.TeamAlreadyOnLastStage);

        // Se captura el tipo de la etapa que se omite para la fila de estadísticas.
        var abandonedStage = stages.FirstOrDefault(s => s.Order == team.CurrentStageOrder);
        var abandonedStageType = abandonedStage?.Type ?? "Trivia";

        var outcome = await _teamClient.ForceAdvanceTeamAsync(request.TeamId, nextOrder, cancellationToken);
        if (outcome is null)
            return Result.Failure<bool>(SessionErrors.CannotForceAdvance);

        // HU-25: WasCorrect es null porque el operador anuló el flujo — no hubo respuesta real.
        var statsRecord = StageCompletionRecord.ForForceAdvance(
            sessionId: request.SessionId,
            missionId: session.MissionId,
            teamId: request.TeamId,
            stageOrder: team.CurrentStageOrder,
            stageType: abandonedStageType,
            elapsedSeconds: outcome.ElapsedSeconds);
        await _statsRepository.AddAsync(statsRecord, cancellationToken);
        await _statsRepository.SaveChangesAsync(cancellationToken);

        // HU-22 / HU-26: registro de auditoría
        await _bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                request.SessionId,
                $"El operador forzó el avance del equipo '{team.Name}' de la etapa {team.CurrentStageOrder} a la etapa {nextOrder}.",
                ActorName: request.OperatorName,
                CommandType: nameof(ForceAdvanceTeamCommand),
                Outcome: SessionEvent.OutcomeSuccess,
                DateTime.UtcNow),
            cancellationToken);

        await _bus.PublishAsync(
            new SessionStateChangedIntegrationEvent(request.SessionId, NewStatus: null),
            cancellationToken);

        return Result.Success(true);
    }
}
