namespace UserService.Application.Users.Commands.DisableUser;

using MediatR;
using UserService.Domain.Common;
using UserService.Domain.Users;

public class DisableUserCommandHandler : IRequestHandler<DisableUserCommand, Result>
{
    private readonly IKeycloakAdminClient _keycloak;

    public DisableUserCommandHandler(IKeycloakAdminClient keycloak) => _keycloak = keycloak;

    public async Task<Result> Handle(DisableUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _keycloak.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result.Failure(UserErrors.NotFound);

        // Idempotente: si ya está deshabilitado, no hacemos nada.
        if (!user.Enabled)
            return Result.Success();

        // ── HU-23 Criterio 4a: no puede deshabilitarse a sí mismo ─────────────
        if (!string.IsNullOrWhiteSpace(request.RequestingUserEmail) &&
            string.Equals(request.RequestingUserEmail.Trim(),
                          user.Email,
                          StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(UserErrors.CannotDisableSelf);
        }

        // ── HU-23 Criterio 4b: no puede ser el último admin activo ───────────
        if (user.Role == UserRole.Admin)
        {
            var allUsers = await _keycloak.ListUsersAsync(cancellationToken);
            var activeAdmins = allUsers.Count(u => u.Role == UserRole.Admin && u.Enabled);
            if (activeAdmins <= 1)
                return Result.Failure(UserErrors.CannotDisableLastAdmin);
        }

        try
        {
            await _keycloak.DisableAsync(request.UserId, cancellationToken);
            return Result.Success();
        }
        catch
        {
            return Result.Failure(UserErrors.KeycloakUnavailable);
        }
    }
}
