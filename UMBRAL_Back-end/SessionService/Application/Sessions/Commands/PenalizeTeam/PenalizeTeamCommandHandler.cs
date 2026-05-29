namespace SessionService.Application.Sessions.Commands.PenalizeTeam;

using MediatR;
using Microsoft.AspNetCore.SignalR;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using SessionService.Infrastructure.Hubs;

public class PenalizeTeamCommandHandler : IRequestHandler<PenalizeTeamCommand, Result<int>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamClient;
    private readonly ISessionEventRepository _eventRepository;
    private readonly IHubContext<SessionHub> _hub;

    public PenalizeTeamCommandHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient,
        ISessionEventRepository eventRepository,
        IHubContext<SessionHub> hub)
    {
        _sessionRepository = sessionRepository;
        _teamClient = teamClient;
        _eventRepository = eventRepository;
        _hub = hub;
    }

    public async Task<Result<int>> Handle(PenalizeTeamCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure<int>(SessionErrors.CannotPenalizeTeam);

        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<int>(SessionErrors.NotFound);

        if (session.Status != SessionStatus.InProgress)
            return Result.Failure<int>(SessionErrors.CannotPenalizeTeam);

        var newScore = await _teamClient.PenalizeTeamAsync(
            request.TeamId, request.Points, request.Reason, cancellationToken);

        if (newScore == int.MinValue)
            return Result.Failure<int>(SessionErrors.CannotPenalizeTeam);

        // Audit log — resolve team name for human-readable message
        var teamInfo = await _teamClient.GetTeamByIdAsync(request.TeamId, cancellationToken);
        var teamName = teamInfo?.Name ?? request.TeamId.ToString();
        var auditEvent = SessionEvent.Create(
            request.SessionId,
            $"Equipo '{teamName}' penalizado {request.Points} pts. Motivo: \"{request.Reason}\". Nuevo puntaje: {newScore}.",
            actorName: request.OperatorName,
            commandType: nameof(PenalizeTeamCommand),
            outcome: SessionEvent.OutcomeSuccess);
        await _eventRepository.AddAsync(auditEvent, cancellationToken);
        await _eventRepository.SaveChangesAsync(cancellationToken);

        // Operator dashboard refresh (audit timeline, ranking, etc.)
        await _hub.Clients
            .Group(request.SessionId.ToString())
            .SendAsync("SessionStateChanged", cancellationToken);

        // HU-28: themed notification for the penalized team. Broadcast to the
        // whole session group; the front filters by TeamId so only the
        // affected participants alza the toast + vibration.
        await _hub.Clients
            .Group(request.SessionId.ToString())
            .SendAsync(
                "TeamPenalized",
                new
                {
                    SessionId  = request.SessionId,
                    TeamId     = request.TeamId,
                    TeamName   = teamName,
                    Points     = request.Points,
                    Reason     = request.Reason,
                    NewScore   = newScore,
                    ActorName  = string.IsNullOrWhiteSpace(request.OperatorName)
                        ? SessionEvent.SystemActor
                        : request.OperatorName!.Trim(),
                },
                cancellationToken);

        return Result.Success(newScore);
    }
}
