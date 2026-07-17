namespace ClueService.Domain.Clues;

using ClueService.Domain.Common;

/// <summary>
/// Value Object for a clue's geographic coordinate (Mission Design context,
/// RB-21). TreasureHunt clues are EXCLUSIVELY visual map revelations — a point
/// plus a radius (see <see cref="GeoRadius"/>). Validates earth-range bounds.
/// The owning <see cref="Clue"/> keeps nullable doubles for EF Core / API.
/// </summary>
public sealed record GeoPoint
{
    public double Latitude { get; }
    public double Longitude { get; }

    private GeoPoint(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public static Result<GeoPoint> Create(double? latitude, double? longitude)
    {
        if (!latitude.HasValue || !longitude.HasValue)
            return Result.Failure<GeoPoint>(ClueErrors.InvalidGeoData);

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            return Result.Failure<GeoPoint>(ClueErrors.InvalidGeoData);

        return Result.Success(new GeoPoint(latitude.Value, longitude.Value));
    }

    public override string ToString() => $"({Latitude}, {Longitude})";
}
