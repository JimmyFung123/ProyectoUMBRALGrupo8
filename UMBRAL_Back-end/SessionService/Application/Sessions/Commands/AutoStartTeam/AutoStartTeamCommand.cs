namespace SessionService.Application.Sessions.Commands.AutoStartTeam;

using MediatR;
using SessionService.Domain.Common;

/// <summary>
/// Auto-arranque de un equipo: si la sesión está en curso y el equipo todavía no
/// entró a ninguna etapa (orden 0), lo avanza a la etapa 1.
///
/// Antes esta ESCRITURA vivía dentro de <c>ParticipantStageFacade.GetCurrentStageAsync</c>
/// (un query) — un efecto secundario en una lectura (CQRS smell). Se extrae al lado de
/// comandos: el endpoint de participante lo envía ANTES del query de lectura, dejando la
/// fachada como lectura pura. Es idempotente y tolerante: si falta cualquier precondición
/// (sesión inexistente, no en curso, equipo inexistente, equipo ya arrancado) no hace nada
/// y devuelve éxito; el query posterior sigue siendo la fuente de verdad de los errores.
/// </summary>
public record AutoStartTeamCommand(Guid SessionId, Guid TeamId) : IRequest<Result>;
