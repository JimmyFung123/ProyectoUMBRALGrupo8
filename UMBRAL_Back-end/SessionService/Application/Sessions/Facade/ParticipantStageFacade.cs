namespace SessionService.Application.Sessions.Facade;

using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Queries.GetParticipantStage;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

/// <summary>
/// Implementación del patrón Facade. Coordina los subsistemas necesarios para
/// armar la vista de la etapa actual de un participante:
///   1. Repositorio de sesiones  → valida que la sesión exista y expone su estado.
///   2. TeamService              → trae el equipo, su orden de etapa y auto-arranca.
///   3. StageService             → lista las etapas de la misión y carga la etapa
///                                 actual con sus opciones.
/// El cliente (el handler de MediatR) solo conoce este punto de entrada único; no
/// necesita saber en qué orden se llaman los servicios, cómo se resuelven los
/// estados "Waiting"/"Completed", ni cómo se oculta el campo <c>IsCorrect</c>.
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

        var currentStageOrder = team.CurrentStageOrder;

        // 3. Auto-start: session is InProgress but team hasn't been assigned a stage yet
        if (session.Status == SessionStatus.InProgress && currentStageOrder == 0)
        {
            await _teamClient.ForceAdvanceTeamAsync(teamId, 1, ct);
            currentStageOrder = 1;
        }

        // 4. Get all stages for the mission
        var stages = await _stageClient.GetStagesByMissionAsync(session.MissionId, ct);
        if (stages.Count == 0)
            return Result.Failure<ParticipantStageDto>(SessionErrors.NotFound);

        var maxOrder = stages.Max(s => s.Order);

        // 5. Team not yet started or session still pending
        if (currentStageOrder == 0)
        {
            return Result.Success(new ParticipantStageDto(
                Guid.Empty,
                "Waiting",
                "Waiting",
                0,
                null,
                [],
                session.Status.ToString(),
                0,
                false));
        }

        // 6. Team has finished all stages (sentinel: currentStageOrder > maxOrder)
        if (currentStageOrder > maxOrder)
        {
            return Result.Success(new ParticipantStageDto(
                Guid.Empty,
                "Completed",
                "Completed",
                currentStageOrder,
                null,
                [],
                session.Status.ToString(),
                currentStageOrder,
                true));
        }

        // 7. Find the current stage record by order
        var stageRef = stages.FirstOrDefault(s => s.Order == currentStageOrder);
        if (stageRef is null)
            return Result.Failure<ParticipantStageDto>(SessionErrors.NotFound);

        // 8. Fetch full stage details with options
        var stageDetails = await _stageClient.GetStageWithOptionsAsync(stageRef.Id, ct);
        if (stageDetails is null)
            return Result.Failure<ParticipantStageDto>(SessionErrors.NotFound);

        // 9. Strip IsCorrect — participants only get Id + Text
        var options = stageDetails.Options
            .Select(o => new ParticipantOptionDto(o.Id, o.Text))
            .ToList();

        bool isLastStage = currentStageOrder == maxOrder;

        // For TreasureHunt stages, expose only Latitude/Longitude (the QrCode stays server-side)
        bool isTreasureHunt = string.Equals(stageDetails.Type, "TreasureHunt", StringComparison.OrdinalIgnoreCase);
        double? latitude = isTreasureHunt ? stageDetails.Latitude : null;
        double? longitude = isTreasureHunt ? stageDetails.Longitude : null;

        return Result.Success(new ParticipantStageDto(
            stageDetails.Id,
            stageDetails.Title,
            stageDetails.Type,
            stageDetails.Order,
            stageDetails.Question,
            options,
            session.Status.ToString(),
            currentStageOrder,
            isLastStage,
            latitude,
            longitude));
    }
}
