namespace TeamService.Domain.Teams;

/// <summary>
/// Value Object for a team's short invite code (Live Session context — the
/// diagram's <c>TeamCode</c>, persisted as <c>Team.InviteCode</c>). A teammate
/// shares this code so others can join the same team (HU-17).
///
/// Encapsulates the generation rule (4 unambiguous chars) that was previously a
/// private helper inside <see cref="Team"/>. The aggregate stores the primitive
/// string so the EF Core / DTO / frontend contract is unchanged.
/// </summary>
public sealed record TeamCode
{
    // Excludes ambiguous characters (0/O, 1/I, etc.) so codes are easy to read aloud.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int Length = 4;

    public string Value { get; }

    private TeamCode(string value) => Value = value;

    /// <summary>Generates a new random invite code.</summary>
    public static TeamCode Generate() =>
        new(new string(Enumerable.Range(0, Length)
            .Select(_ => Alphabet[Random.Shared.Next(Alphabet.Length)]).ToArray()));

    public override string ToString() => Value;
}
