namespace UMBRAL_Back_end.Domain.Missions;

using UMBRAL_Back_end.Domain.Common;

/// <summary>
/// Value Object for a mission's name (Mission Design context).
///
/// Encapsulates the "name cannot be empty" invariant and trims surrounding
/// whitespace so the stored/compared value is canonical. Duplicate-name
/// detection (RB-01 / HU-1) stays in the handler since it needs repository access.
/// </summary>
public sealed record MissionName
{
    public const int MaxLength = 200;

    public string Value { get; }

    private MissionName(string value) => Value = value;

    public static Result<MissionName> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Result.Failure<MissionName>(MissionErrors.InvalidName);

        var trimmed = raw.Trim();
        if (trimmed.Length > MaxLength)
            return Result.Failure<MissionName>(MissionErrors.InvalidName);

        return Result.Success(new MissionName(trimmed));
    }

    public override string ToString() => Value;
}
