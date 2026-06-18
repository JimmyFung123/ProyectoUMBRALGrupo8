namespace UserService.Infrastructure.Email;

/// <summary>
/// SMTP settings, bound from the <c>Smtp</c> section of appsettings.json.
/// In local dev these point at the Mailpit container (localhost:1025, no auth).
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string FromAddress { get; set; } = "no-reply@umbral.local";
    public string FromName { get; set; } = "UMBRAL — Personal Operativo";

    /// <summary>Use STARTTLS. Mailpit in dev runs plaintext, so this is false.</summary>
    public bool UseStartTls { get; set; }

    /// <summary>Empty in dev (Mailpit accepts any/no credentials).</summary>
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
