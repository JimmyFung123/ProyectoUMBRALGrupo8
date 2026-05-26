namespace UserService.Application.Users.Commands.CreateUser;

using MediatR;
using UserService.Domain.Common;
using UserService.Domain.Users;

/// <summary>
/// Registers a new operator/admin in the realm (HU-23, Criterio 1).
/// The temporary password is permanent because the demo realm has the
/// "must change password" requirement disabled — adjust the import realm
/// file to flip that behavior for production.
/// </summary>
public record CreateUserCommand(
    string Email,
    string FirstName,
    string LastName,
    string TemporaryPassword,
    UserRole Role) : IRequest<Result<Guid>>;
