namespace UserService.Application.Users.Commands.CreateUser;

using MediatR;
using UserService.Domain.Common;
using UserService.Domain.Users;

/// <summary>
/// Registers a new operator/admin in the realm (HU-23, Criterio 1).
/// The admin does NOT choose a password: the system generates a strong
/// temporary one, stores it in Keycloak as <c>temporary = true</c> (so the
/// user must change it on first login) and emails it to the new user.
/// </summary>
public record CreateUserCommand(
    string Email,
    string FirstName,
    string LastName,
    UserRole Role) : IRequest<Result<Guid>>;
