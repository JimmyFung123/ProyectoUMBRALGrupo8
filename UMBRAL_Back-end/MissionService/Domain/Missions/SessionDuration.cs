namespace UMBRAL_Back_end.Domain.Missions;

using UMBRAL_Back_end.Domain.Common;

/// <summary>
/// Value Object for a mission's maximum duration in minutes (Mission Design
/// context — the diagram's <c>SessionDuration</c>, persisted as <c>MaxDuration</c>).
///
/// Encapsulates the "must be greater than zero" invariant that previously lived
/// inline in <see cref="Mission"/>.
/// </summary>
public sealed record SessionDuration
{
    /// <summary>Duration in whole minutes.</summary>
    public int Minutes { get; }

    private SessionDuration(int minutes) => Minutes = minutes;

    public static Result<SessionDuration> Create(int minutes)
    {
        if (minutes <= 0)
            return Result.Failure<SessionDuration>(MissionErrors.InvalidMaxDuration);

        return Result.Success(new SessionDuration(minutes));
    }

    public override string ToString() => $"{Minutes} min";
}
