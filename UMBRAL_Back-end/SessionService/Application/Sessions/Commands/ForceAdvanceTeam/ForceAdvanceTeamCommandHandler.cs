namespace SessionService.Application.Sessions.Commands.ForceAdvanceTeam;

using MediatR;
using Microsoft.AspNetCore.SignalR;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using SessionService.Infrastructure.Hubs;

public class ForceAdvanceTeamCommandHandler : IRequestHandler<ForceAdvanceTeamCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamClient;
    private readonly IStageServiceClient _stageClient;
    private readonly ISessionEventRepository _eventRepository;
    private readonly IHubContext<SessionHub> _hub;

    public ForceAdvanceTeamCommandHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient,
        IStageServiceClient stageClient,
        ISessionEventRepository eventRepository,
        IHubContext<SessionHub> hub)
    {
        _sessionRepository = sessionRepository;
        _teamClient = teamClient;
        _stageClient = stageClient;
        _eventRepository = eventRepository;
        _hub = hub;
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

        // 4. Force the advance in TeamService
        var advanced = await _teamClient.ForceAdvanceTeamAsync(request.TeamId, nextOrder, cancellationToken);
        if (!advanced)
            return Result.Failure<bool>(SessionErrors.CannotForceAdvance);

        // 5. Audit log
        var auditEvent = SessionEvent.Create(
            request.SessionId,
            $"El operador forzó el avance del equipo '{team.Name}' de la etapa {team.CurrentStageOrder} a la etapa {nextOrder}.",
            actorName: request.OperatorName);
        await _eventRepository.AddAsync(auditEvent, cancellationToken);
        await _eventRepository.SaveChangesAsync(cancellationToken);

        // 6. Broadcast to refresh dashboard
        await _hub.Clients
            .Group(request.SessionId.ToString())
            .SendAsync("SessionStateChanged", cancellationToken);

        return Result.Success(true);
    }
}
