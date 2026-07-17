namespace UserService.Domain.Users;

using System.Text.RegularExpressions;
using UserService.Domain.Common;

/// <summary>
/// Value Object for an operator/admin email (HU-23, Identity &amp; Access).
///
/// Encapsulates the two invariants that were previously scattered inside
/// <c>CreateUserCommandHandler</c>:
///   * format validation (loose RFC-5322 — Keycloak does its own check on top), and
///   * normalization (trim + lowercase) so the unique-email rule is case-insensitive.
///
/// Authentication and credential storage remain owned by Keycloak; this VO only
/// models the email as a domain concept, never a password.
/// </summary>
public sealed record EmailAddress
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    /// <summary>The normalized (trimmed, lowercased) email.</summary>
    public string Value { get; }

    private EmailAddress(string value) => Value = value;

    /// <summary>
    /// Builds a validated, normalized <see cref="EmailAddress"/> or fails with
    /// <see cref="UserErrors.InvalidEmail"/>. Accepts the raw user input directly.
    /// </summary>
    public static Result<EmailAddress> Create(string? raw)
    {
        var normalized = raw?.Trim().ToLowerInvariant() ?? string.Empty;

        if (!EmailRegex.IsMatch(normalized))
            return Result.Failure<EmailAddress>(UserErrors.InvalidEmail);

        return Result.Success(new EmailAddress(normalized));
    }

    public override string ToString() => Value;
}
