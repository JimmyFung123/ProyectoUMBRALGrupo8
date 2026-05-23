namespace StageService.Domain.Stages;
using StageService.Domain.Common;

public class Stage
{
    public Guid Id { get; private set; }
    public Guid MissionId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public StageType Type { get; private set; }
    public int Order { get; private set; }
    public int BaseScore { get; private set; }

    // Trivia-specific
    public string? Question { get; private set; }

    // TreasureHunt-specific
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

        return Result.Success(new Stage
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            Title = title,
            Type = type,
            Order = order,
            BaseScore = baseScore,
            Question = question,
            Latitude = latitude,
            Longitude = longitude,
            QrCode = qrCode,
            CreatedAt = DateTime.UtcNow
        });
    }

    public void Update(
        string title, int order, int baseScore,
        string? question,
        double? latitude, double? longitude, string? qrCode,
        int? autoReleaseTimeMinutes, int? autoReleaseMaxAttempts)
    {
        Title = title;
        Order = order;
        BaseScore = baseScore;
        Question = question;
        Latitude = latitude;
        Longitude = longitude;
        QrCode = qrCode;
        AutoReleaseTimeMinutes = autoReleaseTimeMinutes;
        AutoReleaseMaxAttempts = autoReleaseMaxAttempts;
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
}
