namespace UserService.Application.Users;

/// <summary>
/// Port for sending account-related emails to operators/admins (HU-23).
/// The implementation lives in <c>Infrastructure/Email</c> and talks SMTP;
/// in local dev it targets the Mailpit catcher (web UI at http://localhost:8025).
/// </summary>
public interface IUserEmailSender
{
    /// <summary>
    /// Emails a freshly created user their system-generated temporary password.
    /// The Keycloak credential is created with <c>temporary = true</c>, so the
    /// user is forced to choose a new password the first time they log in.
    /// </summary>
    Task SendTemporaryPasswordAsync(
        string email,
        string firstName,
        string temporaryPassword,
        CancellationToken cancellationToken);
}
