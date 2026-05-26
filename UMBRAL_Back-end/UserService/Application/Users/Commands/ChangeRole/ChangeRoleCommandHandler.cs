namespace UserService.Application.Users.Commands.ChangeRole;

using MediatR;
using UserService.Domain.Common;
using UserService.Domain.Users;

public class ChangeRoleCommandHandler : IRequestHandler<ChangeRoleCommand, Result>
{
    private readonly IKeycloakAdminClient _keycloak;

    public ChangeRoleCommandHandler(IKeycloakAdminClient keycloak) => _keycloak = keycloak;

    public async Task<Result> Handle(ChangeRoleCommand request, CancellationToken cancellationToken)
    {
        var user = await _keycloak.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result.Failure(UserErrors.NotFound);

        // No-op shortcut: avoid hitting Keycloak twice if the role is already correct.
        if (user.Role == request.NewRole)
            return Result.Success();

        // ── HU-23 Criterio 4: proteger al último administrador ────────────────
        // Si estamos bajando un admin a operator, primero verificamos que no
        // sea el único admin activo del sistema.
        if (user.Role == UserRole.Admin && request.NewRole == UserRole.Operator)
        {
            var allUsers = await _keycloak.ListUsersAsync(cancellationToken);
            var activeAdmins = allUsers.Count(u => u.Role == UserRole.Admin && u.Enabled);
            if (activeAdmins <= 1)
                return Result.Failure(UserErrors.CannotDemoteLastAdmin);
        }

        try
        {
            await _keycloak.ChangeRoleAsync(request.UserId, request.NewRole, cancellationToken);
            return Result.Success();
        }
        catch
        {
            return Result.Failure(UserErrors.KeycloakUnavailable);
        }
    }
}
