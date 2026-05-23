namespace UMBRAL_Back_end.Domain.Missions;

using UMBRAL_Back_end.Domain.Common;

public class MissionStage
{
    private List<TriviaOption> _options = new();
    private List<Clue> _clues = new();

    public Guid Id { get; private set; }
    public Guid MissionId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public StageType Type { get; private set; }
    public int BaseScore { get; private set; }

    // Trivia-specific (null for TreasureHunt)
    public string? Question { get; private set; }
    public IReadOnlyCollection<TriviaOption> Options => _options.AsReadOnly();
    public IReadOnlyCollection<Clue> Clues => _clues.AsReadOnly();

    // TreasureHunt-specific (null for Trivia) — RB-20
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public string? QrCode { get; private set; }

    private MissionStage() { }

    /// <summary>
    /// Creates and validates a new stage. Enforces RB-20 for TreasureHunt stages.
    /// </summary>
    internal static Result<MissionStage> Create(
        Guid missionId,
        string title,
        int order,
        StageType type,
        int baseScore,
        string? question,
        double? latitude,
        double? longitude,
        string? qrCode)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure<MissionStage>(StageErrors.InvalidTitle);

        if (order <= 0)
            return Result.Failure<MissionStage>(StageErrors.InvalidOrder);

        if (baseScore < 0)
            return Result.Failure<MissionStage>(StageErrors.InvalidBaseScore);

        // RB-20: TreasureHunt requires both coordinates AND QR code
        if (type == StageType.TreasureHunt)
        {
            if (!latitude.HasValue || !longitude.HasValue)
                return Result.Failure<MissionStage>(StageErrors.CoordinatesRequired);

            if (string.IsNullOrWhiteSpace(qrCode))
                return Result.Failure<MissionStage>(StageErrors.QrCodeRequired);
        }

        return Result.Success(new MissionStage
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            Title = title.Trim(),
            Order = order,
            Type = type,
            BaseScore = baseScore,
            Question = question?.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            QrCode = qrCode?.Trim()
        });
    }

    /// <summary>Adds a multiple-choice option. Only valid for Trivia stages.</summary>
    internal void AddOption(string text, bool isCorrect)
    {
        if (Type != StageType.Trivia) return;
        _options.Add(TriviaOption.Create(Id, text, isCorrect));
    }

    /// <summary>
    /// Validates that a Trivia options list satisfies business rules:
    /// at least 2 options, exactly 1 marked as correct.
    /// Returns a failure Result if the rules are violated; otherwise Success.
    /// </summary>
    internal static Result ValidateTriviaOptions(IEnumerable<(string Text, bool IsCorrect)> options)
    {
        var list = options.ToList();

        if (list.Count < 2)
            return Result.Failure(StageErrors.TriviaRequiresAtLeastTwoOptions);

        int correctCount = list.Count(o => o.IsCorrect);
        if (correctCount != 1)
            return Result.Failure(StageErrors.TriviaRequiresExactlyOneCorrectOption);

        return Result.Success();
    }

    /// <summary>
    /// Updates the stage's scalar fields. Type cannot change after creation.
    /// Enforces RB-20 for TreasureHunt stages.
    /// </summary>
    internal Result UpdateFields(
        string title,
        int order,
        int baseScore,
        string? question,
        double? latitude,
        double? longitude,
        string? qrCode)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure(StageErrors.InvalidTitle);

        if (order <= 0)
            return Result.Failure(StageErrors.InvalidOrder);

        if (baseScore < 0)
            return Result.Failure(StageErrors.InvalidBaseScore);

        if (Type == StageType.TreasureHunt)
        {
            if (!latitude.HasValue || !longitude.HasValue)
                return Result.Failure(StageErrors.CoordinatesRequired);

            if (string.IsNullOrWhiteSpace(qrCode))
                return Result.Failure(StageErrors.QrCodeRequired);
        }

        Title = title.Trim();
        Order = order;
        BaseScore = baseScore;
        Question = question?.Trim();
        Latitude = latitude;
        Longitude = longitude;
        QrCode = qrCode?.Trim();
        return Result.Success();
    }

    /// <summary>Creates and adds a clue to this stage. Validates duplicate order within the stage.</summary>
    internal Result<Clue> AddClue(
        int order,
        string? content,
        double? latitude,
        double? longitude,
        double? radiusMeters)
    {
        if (_clues.Any(c => c.Order == order))
            return Result.Failure<Clue>(ClueErrors.DuplicateClueOrder);

        var clueResult = Clue.Create(Id, Type, order, content, latitude, longitude, radiusMeters);
        if (clueResult.IsFailure)
            return clueResult;

        _clues.Add(clueResult.Value);
        return clueResult;
    }

    /// <summary>Updates an existing clue. Validates duplicate order, excluding the clue itself.</summary>
    internal Result<Clue> UpdateClue(
        Guid clueId,
        int order,
        string? content,
        double? latitude,
        double? longitude,
        double? radiusMeters)
    {
        var clue = _clues.FirstOrDefault(c => c.Id == clueId);
        if (clue is null)
            return Result.Failure<Clue>(ClueErrors.NotFound);

        if (_clues.Any(c => c.Id != clueId && c.Order == order))
            return Result.Failure<Clue>(ClueErrors.DuplicateClueOrder);

        var updateResult = clue.UpdateFields(Type, order, content, latitude, longitude, radiusMeters);
        if (updateResult.IsFailure)
            return Result.Failure<Clue>(updateResult.Error);

        return Result.Success(clue);
    }

    /// <summary>Removes a clue from this stage.</summary>
    internal Result<Clue> RemoveClue(Guid clueId)
    {
        var clue = _clues.FirstOrDefault(c => c.Id == clueId);
        if (clue is null)
            return Result.Failure<Clue>(ClueErrors.NotFound);

        _clues.Remove(clue);
        return Result.Success(clue);
    }
}
