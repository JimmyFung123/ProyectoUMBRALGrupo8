namespace UserService.Application.Users;

/// <summary>
/// Generates the system-assigned temporary password for a new user (HU-23).
/// The admin never types a password — the system creates a strong random one,
/// stores it in Keycloak as <c>temporary = true</c> and emails it to the user.
/// </summary>
public interface IPasswordGenerator
{
    /// <summary>Returns a fresh, cryptographically-random strong password.</summary>
    string Generate();
}
