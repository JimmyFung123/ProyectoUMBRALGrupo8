namespace UserService.Application.Users.Queries.GetUsers;

/// <summary>
/// Read model of a user for the personnel administration screen (HU-23).
/// Sourced live from Keycloak — UMBRAL does not keep a local copy.
/// </summary>
public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    /// <summary>"admin", "operator", or null for users without an UMBRAL role.</summary>
    string? Role,
    bool Enabled);
