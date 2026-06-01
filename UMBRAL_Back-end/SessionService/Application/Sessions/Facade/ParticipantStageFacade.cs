namespace SessionService.Application.Sessions.Facade;

using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Queries.GetParticipantStage;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

/// <summary>
/// Implementación del patrón Facade. Coordina los subsistemas necesarios para
/// armar la vista de la etapa actual de un participante:
///   1. Repositorio de sesiones  → valida que la sesión exista y expone su estado.
///   2. TeamService              → trae el equipo y su orden de etapa.
///   3. StageService             → lista las etapas de la misión y carga la etapa
///                                 actual con sus opciones.
/// El cliente (el handler de MediatR) solo conoce este punto de entrada único; no
/// necesita saber en qué orden se llaman los servicios ni cómo se resuelven los
/// estados "Waiting"/"Completed".
///
/// Es LECTURA PURA: el auto-arranque del equipo (escritura) ya no vive aquí, lo
/// hace <c>AutoStartTeamCommand</c> antes de invocar este query. El modelado/saneo
/// del DTO (ocultar <c>IsCorrect</c>, coordenadas solo en TreasureHunt) está en
/// <see cref="ParticipantStageMapper"/>.
/// </summary>
public class ParticipantStageFacade : IParticipantStageFacade
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamClient;
    private readonly IStageServiceClient _stageClient;

    public ParticipantStageFacade(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient,
        IStageServiceClient stageClient)
    {
        _sessionRepository = sessionRepository;
        _teamClient = teamClient;
        _stageClient = stageClient;
    }

    public async Task<Result<ParticipantStageDto>> GetCurrentStageAsync(
        Guid sessionId,
        Guid teamId,
        CancellationToken ct)
    {
        // 1. Load session
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
        if (session is null)
            return Result.Failure<ParticipantStageDto>(SessionErrors.NotFound);

        // 2. Get team's current progress
        var team = await _teamClient.GetTeamByIdAsync(teamId, ct);
        if (team is null)
            return Result.Failure<ParticipantStageDto>(SessionErrors.TeamNotFound);

        // Lectura pura: el auto-arranque (escritura) lo hace AutoStartTeamCommand antes
        // de este query, así que aquí solo se refleja el estado actual del equipo.
        var currentStageOrder = team.CurrentStageOrder;

        // 3. Get all stages for the mission
        var stages = await _stageClient.GetStagesByMissionAsync(session.MissionId, ct);
        if (stages.Count == 0)
            return Result.Failure<ParticipantStageDto>(SessionErrors.NotFound);

        var maxOrder = stages.Max(s => s.Order);
        var sessionStatus = session.Status.ToString();

        // 4. Team not yet started (order 0): sentinel "Waiting"
        if (currentStageOrder == 0)
            return Result.Success(ParticipantStageMapper.Waiting(sessionStatus));

        // 5. Team has finished all stages (sentinel: currentStageOrder > maxOrder)
        if (currentStageOrder > maxOrder)
            return Result.Success(ParticipantStageMapper.Completed(sessionStatus, currentStageOrder));

        // 6. Find the current stage record by order
        var stageRef = stages.FirstOrDefault(s => s.Order == currentStageOrder);
        if (stageRef is null)
            return Result.Failure<ParticipantStageDto>(SessionErrors.NotFound);

        // 7. Fetch full stage details with options
        var stageDetails = await _stageClient.GetStageWithOptionsAsync(stageRef.Id, ct);
        if (stageDetails is null)
            return Result.Failure<ParticipantStageDto>(SessionErrors.NotFound);

        // 8. Shape the participant view (hide IsCorrect, gate coordinates by stage type)
        bool isLastStage = currentStageOrder == maxOrder;
        return Result.Success(
            ParticipantStageMapper.FromStage(stageDetails, sessionStatus, currentStageOrder, isLastStage));
    }
}
