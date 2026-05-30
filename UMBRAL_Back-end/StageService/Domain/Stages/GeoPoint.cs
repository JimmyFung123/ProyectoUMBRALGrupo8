namespace StageService.Domain.Stages;

using StageService.Domain.Common;

/// <summary>
/// Value Object for a geographic coordinate (Mission Design context, RB-20).
///
/// Used by TreasureHunt stages to pin the exact location of the treasure.
/// Validates that latitude/longitude fall within valid earth ranges. The
/// owning <see cref="Stage"/> keeps nullable <c>double</c> columns for EF Core
/// and the API contract; this VO is the validation gate at construction time.
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

    public static Result<GeoPoint> Create(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            return Result.Failure<GeoPoint>(StageErrors.InvalidCoordinates);

        return Result.Success(new GeoPoint(latitude, longitude));
    }

    public override string ToString() => $"({Latitude}, {Longitude})";
}
