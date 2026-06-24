namespace UserService.Application.Users;

using UserService.Domain.Common;
using UserService.Domain.Users;

/// <summary>
/// Port from the UMBRAL application layer to Keycloak's Admin REST API
/// (HU-23). The implementation lives in <c>Infrastructure/ExternalClients</c>
/// and authenticates with the <c>umbral-backend</c> service account.
/// </summary>
public interface IKeycloakAdminClient
{
    /// <summary>Returns every user in the realm (active and inactive),
    /// each with its UMBRAL role resolved (admin, operator, or null when
    /// the user has no UMBRAL realm role assigned).</summary>
    Task<IReadOnlyList<KeycloakUser>> ListUsersAsync(CancellationToken cancellationToken);

    /// <summary>Looks up a user by email (case-insensitive). Returns null if
    /// no user matches — used to enforce the unique-email rule (Criterio 1).</summary>
    Task<KeycloakUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<KeycloakUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a user in the realm with a temporary password.
    /// Returns <see cref="Result{Guid}"/> with the new user's id on success,
    /// or <see cref="UserErrors.EmailAlreadyInUse"/> if Keycloak rejects the
    /// request as 409 Conflict (race condition on email uniqueness).
    /// </summary>
    Task<Result<Guid>> CreateUserAsync(
        string email,
        string firstName,
        string lastName,
        string temporaryPassword,
        UserRole role,
        CancellationToken cancellationToken);

    /// <summary>Atomically swaps the user's UMBRAL role (admin ↔ operator).
    /// Both removing the old role and assigning the new one happen in two
    /// HTTP calls — by spec the new role is added FIRST so a transient
    /// failure never leaves the user with no UMBRAL role.</summary>
    Task ChangeRoleAsync(Guid userId, UserRole newRole, CancellationToken cancellationToken);

    /// <summary>Sets <c>enabled = false</c> on the user (HU-23 Criterio 3:
    /// nunca se borra para preservar el historial de auditoría).</summary>
    Task DisableAsync(Guid userId, CancellationToken cancellationToken);

    Task EnableAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Resets the user's password to <paramref name="temporaryPassword"/>
    /// and marks it as temporary so Keycloak forces a change on next login.</summary>
    Task ResetPasswordAsync(Guid userId, string temporaryPassword, CancellationToken cancellationToken);
}

/// <summary>Snapshot of a Keycloak user as UMBRAL cares about it.</summary>
public record KeycloakUser(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool Enabled,
    UserRole? Role);
