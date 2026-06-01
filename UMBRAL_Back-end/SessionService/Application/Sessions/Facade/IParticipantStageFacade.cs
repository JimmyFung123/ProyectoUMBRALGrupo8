namespace SessionService.Application.Sessions.Facade;

using SessionService.Application.Sessions.Queries.GetParticipantStage;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

/// <summary>
/// Facade (GoF) que ofrece una interfaz única y simple para resolver la etapa
/// actual de un equipo participante. Esconde la orquestación de tres subsistemas
/// (repositorio de sesiones, TeamService y StageService). Es lectura pura: el
/// auto-arranque del equipo (escritura) vive en <c>AutoStartTeamCommand</c>.
/// </summary>
public interface IParticipantStageFacade
{
    /// <summary>
    /// Devuelve la etapa que el equipo está jugando ahora mismo (según su orden de
    /// avance), ya mapeada al DTO de participante y sin exponer las respuestas
    /// correctas. Resuelve además los estados centinela "Waiting" (equipo aún no
    /// arrancado) y "Completed" (equipo pasó la última etapa). Falla con
    /// <see cref="SessionErrors.NotFound"/> si la sesión o la etapa no existen, o
    /// con <see cref="SessionErrors.TeamNotFound"/> si el equipo no existe.
    /// </summary>
    Task<Result<ParticipantStageDto>> GetCurrentStageAsync(
        Guid sessionId,
        Guid teamId,
        CancellationToken ct);
}
