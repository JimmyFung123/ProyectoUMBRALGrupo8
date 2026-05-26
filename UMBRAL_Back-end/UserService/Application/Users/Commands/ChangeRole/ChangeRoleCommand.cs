namespace UserService.Application.Users.Commands.ChangeRole;

using MediatR;
using UserService.Domain.Common;
using UserService.Domain.Users;

/// <summary>
/// HU-23 Criterio 2: ascender/degradar un usuario entre Admin y Operator.
/// El cambio se refleja inmediatamente al re-validar el token en próximos
/// requests (Keycloak emite los roles en cada nuevo access_token).
/// </summary>
public record ChangeRoleCommand(Guid UserId, UserRole NewRole) : IRequest<Result>;
