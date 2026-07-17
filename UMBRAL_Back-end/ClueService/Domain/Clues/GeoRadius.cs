namespace ClueService.Domain.Clues;

using ClueService.Domain.Common;

/// <summary>
/// Value Object for the approximation radius (in meters) of a TreasureHunt clue
/// (Mission Design context, RB-21). As clues are released the radius shrinks to
/// guide the team toward the exact spot. Must be a positive number of meters.
/// </summary>
public sealed record GeoRadius
{
    public int Meters { get; }

    private GeoRadius(int meters) => Meters = meters;

    public static Result<GeoRadius> Create(int? meters)
    {
        if (!meters.HasValue || meters.Value <= 0)
            return Result.Failure<GeoRadius>(ClueErrors.InvalidGeoData);

        return Result.Success(new GeoRadius(meters.Value));
    }

    public override string ToString() => $"{Meters} m";
}
