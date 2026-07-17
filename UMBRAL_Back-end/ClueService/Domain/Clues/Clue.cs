namespace ClueService.Domain.Clues;
using ClueService.Domain.Common;
using ClueService.Domain.Clues.Events;

public class Clue : AggregateRoot
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

        var createdAt = DateTime.UtcNow;
        var normalizedContent = string.IsNullOrWhiteSpace(content) ? null : content;
        var clue = new Clue
        {
            Id = Guid.NewGuid(),
            StageId = stageId,
            MissionId = missionId,
            StageType = NormalizeStageType(stageType),
            Order = order,
            Content = normalizedContent,
            Latitude = latitude,
            Longitude = longitude,
            RadiusMeters = radiusMeters,
            AutoReleaseAfterMinutes = autoReleaseAfterMinutes,
            CreatedAt = createdAt
        };
        clue.AddDomainEvent(new ClueAddedDomainEvent(
            clue.Id, stageId, missionId, normalizedContent, latitude, longitude, radiusMeters, createdAt));
        return Result.Success(clue);
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

    /// <summary>Marks this clue for removal, raising the domain event the
    /// caller's repository.DeleteAsync(...) call is about to make true.</summary>
    public void MarkForRemoval() => AddDomainEvent(new ClueRemovedDomainEvent(Id, StageId, MissionId, DateTime.UtcNow));

    private static Result<bool> ValidatePayloadForStageType(
        string stageType, string? content, double? latitude, double? longitude, int? radiusMeters)
    {
        var normalized = NormalizeStageType(stageType);

        if (normalized == "TreasureHunt")
        {
            var geoResult = GeoPoint.Create(latitude, longitude);
            if (geoResult.IsFailure) return Result.Failure<bool>(geoResult.Error);

            var radiusResult = GeoRadius.Create(radiusMeters);
            if (radiusResult.IsFailure) return Result.Failure<bool>(radiusResult.Error);

            if (string.IsNullOrWhiteSpace(content))
                return Result.Failure<bool>(ClueErrors.InvalidContent);

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
