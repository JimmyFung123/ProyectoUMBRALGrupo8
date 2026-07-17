namespace UMBRAL_Back_end.Domain.Missions;

using UMBRAL_Back_end.Domain.Common;

/// <summary>
/// Value Object for a mission's description (Mission Design context).
///
/// Optional free text (a mission may have an empty description), but capped in
/// length to match the persistence column and trimmed for consistency.
/// </summary>
public sealed record MissionDescription
{
    public const int MaxLength = 1000;

    public string Value { get; }

    private MissionDescription(string value) => Value = value;

    public static Result<MissionDescription> Create(string? raw)
    {
        var trimmed = raw?.Trim() ?? string.Empty;
        if (trimmed.Length > MaxLength)
            return Result.Failure<MissionDescription>(MissionErrors.InvalidDescription);

        return Result.Success(new MissionDescription(trimmed));
    }

    public override string ToString() => Value;
}
