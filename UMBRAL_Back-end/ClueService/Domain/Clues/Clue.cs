namespace ClueService.Domain.Clues;
using ClueService.Domain.Common;

public class Clue
{
    public Guid Id { get; private set; }
    public Guid StageId { get; private set; }
    public Guid MissionId { get; private set; }
    public string StageType { get; private set; } = "Trivia";
    public string? Content { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public int? RadiusMeters { get; private set; }
    public int Order { get; private set; }
    public int? AutoReleaseAfterMinutes { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Clue() { }

    public static Result<Clue> Create(
        Guid stageId,
        Guid missionId,
        string stageType,
        int order,
        string? content,
        double? latitude,
        double? longitude,
        int? radiusMeters,
        int? autoReleaseAfterMinutes = null)
    {
        if (order <= 0)
            return Result.Failure<Clue>(ClueErrors.InvalidOrder);

        var validation = ValidatePayloadForStageType(stageType, content, latitude, longitude, radiusMeters);
        if (validation.IsFailure)
            return Result.Failure<Clue>(validation.Error);

        return Result.Success(new Clue
        {
            Id = Guid.NewGuid(),
            StageId = stageId,
            MissionId = missionId,
            StageType = NormalizeStageType(stageType),
            Order = order,
            Content = string.IsNullOrWhiteSpace(content) ? null : content,
            Latitude = latitude,
            Longitude = longitude,
            RadiusMeters = radiusMeters,
            AutoReleaseAfterMinutes = autoReleaseAfterMinutes,
            CreatedAt = DateTime.UtcNow
        });
    }

    // Legacy overload kept for back-compat with older callers (tests/migrations).
    public static Result<Clue> Create(Guid stageId, Guid missionId, string content, int order, int? autoReleaseAfterMinutes = null)
        => Create(stageId, missionId, "Trivia", order, content, null, null, null, autoReleaseAfterMinutes);

    public Result<bool> Update(int order, string? content, double? latitude, double? longitude, int? radiusMeters)
    {
        if (order <= 0)
            return Result.Failure<bool>(ClueErrors.InvalidOrder);

        var validation = ValidatePayloadForStageType(StageType, content, latitude, longitude, radiusMeters);
        if (validation.IsFailure) return Result.Failure<bool>(validation.Error);

        Order = order;
        Content = string.IsNullOrWhiteSpace(content) ? null : content;
        Latitude = latitude;
        Longitude = longitude;
        RadiusMeters = radiusMeters;
        return Result.Success(true);
    }

    public void SetAutoRelease(int? minutes) => AutoReleaseAfterMinutes = minutes;

    private static Result<bool> ValidatePayloadForStageType(
        string stageType, string? content, double? latitude, double? longitude, int? radiusMeters)
    {
        var normalized = NormalizeStageType(stageType);

        if (normalized == "TreasureHunt")
        {
            if (latitude is null || longitude is null || radiusMeters is null || radiusMeters <= 0)
                return Result.Failure<bool>(ClueErrors.InvalidGeoData);
            return Result.Success(true);
        }

        // Default to Trivia rules for any other stage type — content required.
        if (string.IsNullOrWhiteSpace(content))
            return Result.Failure<bool>(ClueErrors.InvalidContent);
        return Result.Success(true);
    }

    private static string NormalizeStageType(string stageType)
    {
        if (string.Equals(stageType, "TreasureHunt", StringComparison.OrdinalIgnoreCase))
            return "TreasureHunt";
        return "Trivia";
    }
}
