namespace UserService.Domain.Users;

/// <summary>
/// Domain Service for Identity &amp; Access role rules (HU-23 Criterio 4).
///
/// Centralizes the guards that were previously duplicated/inlined across
/// <c>ChangeRoleCommandHandler</c> and <c>DisableUserCommandHandler</c>:
///   * detecting an Admin→Operator demotion,
///   * counting active administrators and deciding if one is "the last", and
///   * detecting a self-targeting action (by case-insensitive email).
///
/// Pure domain logic with no I/O and no dependency on the Application layer:
/// it receives already-loaded data (primitives / tuples) so the handlers stay
/// thin orchestrators that decide *when* to hit Keycloak, while the *rules*
/// live here and are trivially unit-testable. "Active admin" = Admin AND enabled.
/// </summary>
public static class RolePolicy
{
    /// <summary>True when the change drops an Admin down to Operator (the only
    /// transition that risks leaving the system without administrators).</summary>
    public static bool IsDemotion(UserRole? currentRole, UserRole newRole)
        => currentRole == UserRole.Admin && newRole == UserRole.Operator;

    /// <summary>Counts active administrators (role Admin and enabled) in a population.</summary>
    public static int CountActiveAdmins(IEnumerable<(UserRole? Role, bool Enabled)> users)
        => users.Count(u => u.Role == UserRole.Admin && u.Enabled);

    /// <summary>True when removing/disabling one more admin would leave none active.</summary>
    public static bool IsLastActiveAdmin(int activeAdminCount) => activeAdminCount <= 1;

    /// <summary>True when the requester is acting on their own account
    /// (email comparison is case-insensitive and trims the requester input).</summary>
    public static bool IsSelf(string targetEmail, string? requesterEmail)
        => !string.IsNullOrWhiteSpace(requesterEmail)
           && string.Equals(requesterEmail.Trim(), targetEmail, StringComparison.OrdinalIgnoreCase);
}
