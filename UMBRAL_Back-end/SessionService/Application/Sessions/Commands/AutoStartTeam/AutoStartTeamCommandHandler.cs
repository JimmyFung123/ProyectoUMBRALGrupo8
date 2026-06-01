namespace SessionService.Application.Sessions.Commands.AutoStartTeam;

using MediatR;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

/// <summary>
/// Maneja <see cref="AutoStartTeamCommand"/>. Única responsabilidad: la regla de
/// negocio de auto-arrancar un equipo. La fachada de lectura ya no la conoce.
/// </summary>
public class AutoStartTeamCommandHandler : IRequestHandler<AutoStartTeamCommand, Result>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamClient;

    public AutoStartTeamCommandHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient)
    {
        _sessionRepository = sessionRepository;
        _teamClient = teamClient;
    }

    public async Task<Result> Handle(AutoStartTeamCommand request, CancellationToken ct)
    {
        // Pre-paso best-effort: nunca falla la petición del participante. El query de
        // lectura posterior es la fuente de verdad de NotFound / TeamNotFound.

        // Solo tiene sentido auto-arrancar con la sesión en curso. Si no, no se toca
        // TeamService (se evita una llamada HTTP en cada poll mientras está Pending/Paused).
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, ct);
        if (session is null || session.Status != SessionStatus.InProgress)
            return Result.Success();

        var team = await _teamClient.GetTeamByIdAsync(request.TeamId, ct);
        if (team is null || team.CurrentStageOrder != 0)
            return Result.Success();

        // El equipo está inscrito pero aún no entró a la primera etapa → arrancarlo.
        await _teamClient.ForceAdvanceTeamAsync(request.TeamId, 1, ct);
        return Result.Success();
    }
}
