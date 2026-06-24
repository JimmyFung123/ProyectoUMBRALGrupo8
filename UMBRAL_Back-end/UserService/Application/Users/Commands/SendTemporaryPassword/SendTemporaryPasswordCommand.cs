namespace UserService.Application.Users.Commands.SendTemporaryPassword;

using MediatR;
using UserService.Domain.Common;

/// <summary>
/// Genera una nueva clave temporal para el usuario, la resetea en Keycloak
/// (temporal=true, fuerza cambio en el próximo login) y la envía por correo.
/// Útil cuando el usuario perdió o nunca recibió la clave inicial.
/// </summary>
public record SendTemporaryPasswordCommand(Guid UserId) : IRequest<Result>;
