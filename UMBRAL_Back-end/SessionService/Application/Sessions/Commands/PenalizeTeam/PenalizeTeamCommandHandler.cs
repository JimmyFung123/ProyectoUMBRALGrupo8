namespace SessionService.Application.Sessions.Commands.PenalizeTeam;

using MediatR;
using SessionService.Application;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using UMBRAL.Contracts.Events;

public class PenalizeTeamCommandHandler : IRequestHandler<PenalizeTeamCommand, Result<int>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamClient;
    private readonly IIntegrationEventBus _bus;

    public PenalizeTeamCommandHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient,
        IIntegrationEventBus bus)
    {
        _sessionRepository = sessionRepository;
        _teamClient = teamClient;
        _bus = bus;
    }

    public async Task<Result<int>> Handle(PenalizeTeamCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure<int>(SessionErrors.CannotPenalizeTeam);

        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<int>(SessionErrors.NotFound);

        if (!SessionAvailabilityPolicy.IsInProgress(session.Status))
            return Result.Failure<int>(SessionErrors.CannotPenalizeTeam);

        var newScore = await _teamClient.PenalizeTeamAsync(
            request.TeamId, request.Points, request.Reason, cancellationToken);

        if (newScore == int.MinValue)
            return Result.Failure<int>(SessionErrors.CannotPenalizeTeam);

        // Audit log — resolve team name for human-readable message
        var teamInfo = await _teamClient.GetTeamByIdAsync(request.TeamId, cancellationToken);
        var teamName = teamInfo?.Name ?? request.TeamId.ToString();
        await _bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                request.SessionId,
                $"Equipo '{teamName}' penalizado {request.Points} pts. Motivo: \"{request.Reason}\". Nuevo puntaje: {newScore}.",
                ActorName: request.OperatorName,
                CommandType: nameof(PenalizeTeamCommand),
                Outcome: SessionEvent.OutcomeSuccess,
                DateTime.UtcNow),
            cancellationToken);

        var actorName = ActorNameResolver.Resolve(request.OperatorName);

        await _bus.PublishAsync(
            new TeamPenalizedIntegrationEvent(
                request.SessionId, request.TeamId, teamName,
                request.Points, request.Reason, newScore, actorName),
            cancellationToken);

        return Result.Success(newScore);
    }
}
