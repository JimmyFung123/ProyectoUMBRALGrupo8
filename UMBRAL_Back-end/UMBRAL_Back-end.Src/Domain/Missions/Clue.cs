namespace UMBRAL_Back_end.Domain.Missions;

using UMBRAL_Back_end.Domain.Common;

public class Clue
{
    public Guid Id { get; private set; }
    public Guid StageId { get; private set; }
    public int Order { get; private set; }

    // Trivia-specific (null for TreasureHunt)
    public string? Content { get; private set; }

    // TreasureHunt-specific (null for Trivia)
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public double? RadiusMeters { get; private set; }

    private Clue() { }

    internal static Result<Clue> Create(
        Guid stageId,
        StageType stageType,
        int order,
        string? content,
        double? latitude,
        double? longitude,
        double? radiusMeters)
    {
        if (order <= 0)
            return Result.Failure<Clue>(ClueErrors.InvalidOrder);

        if (stageType == StageType.Trivia)
        {
            if (string.IsNullOrWhiteSpace(content))
                return Result.Failure<Clue>(ClueErrors.ContentRequired);
        }
        else
        {
            if (!latitude.HasValue || !longitude.HasValue)
                return Result.Failure<Clue>(ClueErrors.CoordinatesRequired);

            if (!radiusMeters.HasValue)
                return Result.Failure<Clue>(ClueErrors.RadiusRequired);
        }

        return Result.Success(new Clue
        {
            Id = Guid.NewGuid(),
            StageId = stageId,
            Order = order,
            Content = stageType == StageType.Trivia ? content!.Trim() : null,
            Latitude = stageType == StageType.TreasureHunt ? latitude : null,
            Longitude = stageType == StageType.TreasureHunt ? longitude : null,
            RadiusMeters = stageType == StageType.TreasureHunt ? radiusMeters : null
        });
    }

    internal Result UpdateFields(
        StageType stageType,
        int order,
        string? content,
        double? latitude,
        double? longitude,
        double? radiusMeters)
    {
        if (order <= 0)
            return Result.Failure(ClueErrors.InvalidOrder);

        if (stageType == StageType.Trivia)
        {
            if (string.IsNullOrWhiteSpace(content))
                return Result.Failure(ClueErrors.ContentRequired);
        }
        else
        {
            if (!latitude.HasValue || !longitude.HasValue)
                return Result.Failure(ClueErrors.CoordinatesRequired);

            if (!radiusMeters.HasValue)
                return Result.Failure(ClueErrors.RadiusRequired);
        }

        Order = order;
        Content = stageType == StageType.Trivia ? content!.Trim() : null;
        Latitude = stageType == StageType.TreasureHunt ? latitude : null;
        Longitude = stageType == StageType.TreasureHunt ? longitude : null;
        RadiusMeters = stageType == StageType.TreasureHunt ? radiusMeters : null;
        return Result.Success();
    }
}
