namespace TeamService.Domain.Teams;

using TeamService.Domain.Common;

/// <summary>
/// Value Object for a team's display name (Live Session context, HU-17).
///
/// Participants type this when creating a team, so it is validated at the
/// application boundary (CreateTeamCommandHandler) before the <see cref="Team"/>
/// aggregate is built. Cannot be empty; trimmed for consistency. The aggregate
/// keeps a primitive <c>string</c> property so the EF Core / DTO / frontend
/// contract is unchanged.
/// </summary>
public sealed record TeamName
{
    public const int MaxLength = 60;

    public string Value { get; }

    private TeamName(string value) => Value = value;

    public static Result<TeamName> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Result.Failure<TeamName>(TeamErrors.InvalidTeamName);

        var trimmed = raw.Trim();
        if (trimmed.Length > MaxLength)
            return Result.Failure<TeamName>(TeamErrors.InvalidTeamName);

        return Result.Success(new TeamName(trimmed));
    }

    public override string ToString() => Value;
}
