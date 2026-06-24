namespace SessionService.Application.Sessions.Commands.LeaveTeam;

using MediatR;
using SessionService.Application;
using SessionService.Domain.Common;
using UMBRAL.Contracts.Events;

/// <summary>
/// Maneja <see cref="LeaveTeamCommand"/>. Solo reenvía la baja del participante a
/// TeamService — SessionService no modela miembros, solo orquesta la llamada.
/// Publica SessionStateChanged para que el dashboard del operador se refresque en
/// tiempo real (mismo evento "catch-all" que ya usan Pause/Resume/Finalize — el
/// front lo trata como "algo cambió, refrescá", sin leer su contenido).
/// </summary>
public class LeaveTeamCommandHandler : IRequestHandler<LeaveTeamCommand, Result>
{
    private readonly ITeamServiceClient _teamClient;
    private readonly IIntegrationEventBus _bus;

    public LeaveTeamCommandHandler(ITeamServiceClient teamClient, IIntegrationEventBus bus)
    {
        _teamClient = teamClient;
        _bus = bus;
    }

    public async Task<Result> Handle(LeaveTeamCommand request, CancellationToken cancellationToken)
    {
        await _teamClient.LeaveTeamAsync(request.TeamId, cancellationToken);

        await _bus.PublishAsync(
            new SessionStateChangedIntegrationEvent(request.SessionId, null),
            cancellationToken);

        return Result.Success();
    }
}
