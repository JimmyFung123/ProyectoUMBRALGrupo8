namespace UserService.Application.Users.Commands.DisableUser;

using MediatR;
using UserService.Domain.Common;

/// <summary>
/// HU-23 Criterio 3: deshabilitar = poner enabled=false en Keycloak.
/// Nunca se borra para preservar el historial de auditoría (HU-22).
///
/// <paramref name="RequestingUserEmail"/> viene del JWT del administrador
/// que ejecuta la acción — se usa para impedir auto-desactivación (Criterio 4).
/// </summary>
public record DisableUserCommand(Guid UserId, string? RequestingUserEmail) : IRequest<Result>;
