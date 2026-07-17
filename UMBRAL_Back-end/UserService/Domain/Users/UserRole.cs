namespace UserService.Domain.Users;

/// <summary>
/// Operational personnel roles in UMBRAL (HU-23).
/// Mapped 1:1 to Keycloak realm roles with the same names.
/// </summary>
public enum UserRole
{
    Operator = 0,
    Admin    = 1,
}

public static class UserRoleExtensions
{
    /// <summary>String value used by Keycloak's realm role names.</summary>
    public static string ToKeycloakName(this UserRole role) => role switch
    {
        UserRole.Admin    => "admin",
        UserRole.Operator => "operator",
        _                 => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    /// <summary>Parses Keycloak's realm role name back into the enum. Returns null
    /// for roles UMBRAL does not care about (default realm roles, etc.).</summary>
    public static UserRole? FromKeycloakName(string roleName) => roleName switch
    {
        "admin"    => UserRole.Admin,
        "operator" => UserRole.Operator,
        _          => null,
    };
}
