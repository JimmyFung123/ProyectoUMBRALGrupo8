namespace StageService.Domain.Stages;
using StageService.Domain.Common;
using StageService.Domain.Stages.Events;

/// <summary>
/// Aggregate Root of a mission stage (Mission Design context). Unifies the
/// diagram's MissionStage / TriviaStage / TreasureStage via <see cref="StageType"/>
/// + nullable type-specific fields. Owned by StageService; the parent mission is
/// referenced by id and its Active status is mirrored via MissionLookup (RB-14).
///
/// Input validation is delegated to the value objects <see cref="StageOrder"/>,
/// <see cref="ScoreValue"/>, <see cref="GeoPoint"/> and <see cref="QrCode"/>.
/// RB-20: a TreasureHunt stage REQUIRES both coordinates and a QR code. Public
/// properties stay primitive so EF Core mapping, DTOs and the frontend contract
/// remain unchanged.
/// </summary>
public class Stage : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid MissionId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public StageType Type { get; private set; }
    public int Order { get; private set; }
    public int BaseScore { get; private set; }

    // Trivia-specific
    public string? Question { get; private set; }

    // TreasureHunt-specific (RB-20)
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public string? QrCode { get; private set; }

    // Auto-release rule
    public int? AutoReleaseTimeMinutes { get; private set; }
    public int? AutoReleaseMaxAttempts { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public List<TriviaOption> Options { get; private set; } = [];

    private Stage() { }

    public static Result<Stage> Create(
        Guid missionId, string title, StageType type, int order, int baseScore,
        string? question = null,
        double? latitude = null, double? longitude = null, string? qrCode = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure<Stage>(StageErrors.InvalidName);

        var orderResult = StageOrder.Create(order);
        if (orderResult.IsFailure) return Result.Failure<Stage>(orderResult.Error);

        var scoreResult = ScoreValue.Create(baseScore);
        if (scoreResult.IsFailure) return Result.Failure<Stage>(scoreResult.Error);

        var treasure = ValidateTreasure(type, latitude, longitude, qrCode);
        if (treasure.IsFailure) return Result.Failure<Stage>(treasure.Error);

        var createdAt = DateTime.UtcNow;
        var stage = new Stage
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            Title = title.Trim(),
            Type = type,
            Order = orderResult.Value.Value,
            BaseScore = scoreResult.Value.Points,
            Question = question?.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            QrCode = qrCode?.Trim(),
            CreatedAt = createdAt
        };
        stage.AddDomainEvent(new StageAddedDomainEvent(stage.Id, missionId, type, createdAt));
        return Result.Success(stage);
    }

    public Result<bool> Update(
        string title, int order, int baseScore,
        string? question,
        double? latitude, double? longitude, string? qrCode,
        int? autoReleaseTimeMinutes, int? autoReleaseMaxAttempts)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure<bool>(StageErrors.InvalidName);

        var orderResult = StageOrder.Create(order);
        if (orderResult.IsFailure) return Result.Failure<bool>(orderResult.Error);

        var scoreResult = ScoreValue.Create(baseScore);
        if (scoreResult.IsFailure) return Result.Failure<bool>(scoreResult.Error);

        // Type cannot change after creation, so re-validate RB-20 against the
        // stage's existing Type, not a caller-supplied one.
        var treasure = ValidateTreasure(Type, latitude, longitude, qrCode);
        if (treasure.IsFailure) return Result.Failure<bool>(treasure.Error);

        Title = title.Trim();
        Order = orderResult.Value.Value;
        BaseScore = scoreResult.Value.Points;
        Question = question?.Trim();
        Latitude = latitude;
        Longitude = longitude;
        QrCode = qrCode?.Trim();
        AutoReleaseTimeMinutes = autoReleaseTimeMinutes;
        AutoReleaseMaxAttempts = autoReleaseMaxAttempts;
        return Result.Success(true);
    }

    public void SetAutoRelease(int? timeMinutes, int? maxAttempts)
    {
        AutoReleaseTimeMinutes = timeMinutes;
        AutoReleaseMaxAttempts = maxAttempts;
    }

    public void ReplaceOptions(IEnumerable<(string Text, bool IsCorrect)> options)
    {
        Options.Clear();
        foreach (var (text, isCorrect) in options)
            Options.Add(new TriviaOption(Guid.NewGuid(), Id, text, isCorrect));
    }

    /// <summary>
    /// RB-20: a TreasureHunt stage requires both valid coordinates (via
    /// <see cref="GeoPoint"/>) and a QR code (via <see cref="QrCode"/>). Trivia
    /// stages skip this check entirely.
    /// </summary>
    private static Result<bool> ValidateTreasure(StageType type, double? latitude, double? longitude, string? qrCode)
    {
        if (type != StageType.TreasureHunt)
            return Result.Success(true);

        if (!latitude.HasValue || !longitude.HasValue)
            return Result.Failure<bool>(StageErrors.CoordinatesRequired);

        var geoResult = GeoPoint.Create(latitude.Value, longitude.Value);
        if (geoResult.IsFailure) return Result.Failure<bool>(geoResult.Error);

        var qrResult = QrCodeValue.Create(qrCode);
        if (qrResult.IsFailure) return Result.Failure<bool>(qrResult.Error);

        return Result.Success(true);
    }
}
