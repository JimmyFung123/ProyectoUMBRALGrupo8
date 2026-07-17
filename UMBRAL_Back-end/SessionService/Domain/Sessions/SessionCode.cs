namespace SessionService.Domain.Sessions;

/// <summary>
/// Value Object for a session's access code (Live Session context — the
/// diagram's <c>SessionCode</c>, persisted as <c>Session.AccessCode</c>).
/// Participants type this PIN to find and join a session (HU-17).
///
/// Encapsulates the generation rule (6 alphanumeric chars) that was previously a
/// private helper inside <see cref="Session"/>. The aggregate stores the
/// primitive string so the EF Core / DTO / frontend contract is unchanged.
/// </summary>
public sealed record SessionCode
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    public const int Length = 6;

    public string Value { get; }

    private SessionCode(string value) => Value = value;

    /// <summary>Generates a new random access code.</summary>
    public static SessionCode Generate() =>
        new(new string(Enumerable.Range(0, Length)
            .Select(_ => Alphabet[Random.Shared.Next(Alphabet.Length)]).ToArray()));

    public override string ToString() => Value;
}
