namespace UMBRAL_Back_end.Domain.Missions;

using UMBRAL_Back_end.Domain.Common;

public class Mission
{
    private List<MissionStage> _stages = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DifficultyLevel Difficulty { get; private set; }
    public int MaxDuration { get; private set; }
    public MissionStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public IReadOnlyCollection<MissionStage> Stages => _stages.AsReadOnly();

    private Mission() { }

    private Mission(Guid id, string name, string description, DifficultyLevel difficulty, int maxDuration)
    {
        Id = id;
        Name = name;
        Description = description;
        Difficulty = difficulty;
        MaxDuration = maxDuration;
        Status = MissionStatus.Inactive;
        CreatedAt = DateTime.UtcNow;
    }

    public static Result<Mission> Create(string name, string description, DifficultyLevel difficulty, int maxDuration)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Mission>(MissionErrors.InvalidName);

        if (maxDuration <= 0)
            return Result.Failure<Mission>(MissionErrors.InvalidMaxDuration);

        return Result.Success(new Mission(Guid.NewGuid(), name.Trim(), description, difficulty, maxDuration));
    }

    /// <summary>
    /// Transitions to Active. Fails if no stages are configured (RB-13).
    /// </summary>
    public Result Activate()
    {
        if (_stages.Count == 0)
            return Result.Failure(MissionErrors.NoStages);

        Status = MissionStatus.Active;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Transitions to Inactive. Fails if active sessions exist (RB-15).
    /// </summary>
    public Result Deactivate(bool hasActiveSessions)
    {
        if (hasActiveSessions)
            return Result.Failure(MissionErrors.HasActiveSessions);

        Status = MissionStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Updates mission metadata. Fails if active sessions exist (RB-14).
    /// </summary>
    public Result Update(string name, string description, DifficultyLevel difficulty, int maxDuration, bool hasActiveSessions)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(MissionErrors.InvalidName);

        if (maxDuration <= 0)
            return Result.Failure(MissionErrors.InvalidMaxDuration);

        if (hasActiveSessions)
            return Result.Failure(MissionErrors.HasActiveSessions);

        Name = name.Trim();
        Description = description;
        Difficulty = difficulty;
        MaxDuration = maxDuration;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Adds a stage to this mission.
    /// RB-14: Blocked if mission is Active OR has sessions in progress.
    /// </summary>
    public Result<MissionStage> AddStage(
        string title,
        int order,
        StageType type,
        int baseScore,
        bool hasActiveSessions,
        string? question = null,
        IEnumerable<(string Text, bool IsCorrect)>? options = null,
        double? latitude = null,
        double? longitude = null,
        string? qrCode = null)
    {
        if (Status == MissionStatus.Active || hasActiveSessions)
            return Result.Failure<MissionStage>(StageErrors.MissionLockedForEditing);

        if (_stages.Any(s => s.Order == order))
            return Result.Failure<MissionStage>(StageErrors.DuplicateStageOrder);

        if (type == StageType.Trivia && options is not null)
        {
            var triviaValidation = MissionStage.ValidateTriviaOptions(options);
            if (triviaValidation.IsFailure)
                return Result.Failure<MissionStage>(triviaValidation.Error);
        }

        var stageResult = MissionStage.Create(Id, title, order, type, baseScore, question, latitude, longitude, qrCode);
        if (stageResult.IsFailure)
            return stageResult;

        var stage = stageResult.Value;

        if (options is not null)
            foreach (var (text, isCorrect) in options)
                stage.AddOption(text, isCorrect);

        _stages.Add(stage);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success(stage);
    }

    /// <summary>
    /// Updates an existing stage's fields.
    /// RB-14: Blocked if mission is Active OR has sessions in progress.
    /// </summary>
    public Result<MissionStage> UpdateStage(
        Guid stageId,
        bool hasActiveSessions,
        string title,
        int order,
        int baseScore,
        string? question,
        double? latitude,
        double? longitude,
        string? qrCode,
        IEnumerable<(string Text, bool IsCorrect)>? options = null)
    {
        if (Status == MissionStatus.Active || hasActiveSessions)
            return Result.Failure<MissionStage>(StageErrors.MissionLockedForEditing);

        var stage = _stages.FirstOrDefault(s => s.Id == stageId);
        if (stage is null)
            return Result.Failure<MissionStage>(StageErrors.NotFound);

        // Order duplicate check: another stage (not the one being updated) with the same order
        if (_stages.Any(s => s.Id != stageId && s.Order == order))
            return Result.Failure<MissionStage>(StageErrors.DuplicateStageOrder);

        if (stage.Type == StageType.Trivia && options is not null)
        {
            var triviaValidation = MissionStage.ValidateTriviaOptions(options);
            if (triviaValidation.IsFailure)
                return Result.Failure<MissionStage>(triviaValidation.Error);
        }

        var updateResult = stage.UpdateFields(title, order, baseScore, question, latitude, longitude, qrCode);
        if (updateResult.IsFailure)
            return Result.Failure<MissionStage>(updateResult.Error);

        UpdatedAt = DateTime.UtcNow;
        return Result.Success(stage);
    }

    /// <summary>
    /// Removes a stage from this mission.
    /// RB-14: Blocked if mission is Active OR has sessions in progress.
    /// </summary>
    public Result RemoveStage(Guid stageId, bool hasActiveSessions)
    {
        if (Status == MissionStatus.Active || hasActiveSessions)
            return Result.Failure(StageErrors.MissionLockedForEditing);

        var stage = _stages.FirstOrDefault(s => s.Id == stageId);
        if (stage is null)
            return Result.Failure(StageErrors.NotFound);

        _stages.Remove(stage);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Adds a clue to a stage.
    /// RB-14: Blocked if mission is Active OR has sessions in progress.
    /// </summary>
    public Result<Clue> AddClue(
        Guid stageId,
        bool hasActiveSessions,
        int order,
        string? content,
        double? latitude,
        double? longitude,
        double? radiusMeters)
    {
        if (Status == MissionStatus.Active || hasActiveSessions)
            return Result.Failure<Clue>(ClueErrors.StageLocked);

        var stage = _stages.FirstOrDefault(s => s.Id == stageId);
        if (stage is null)
            return Result.Failure<Clue>(StageErrors.NotFound);

        var result = stage.AddClue(order, content, latitude, longitude, radiusMeters);
        if (result.IsFailure)
            return result;

        UpdatedAt = DateTime.UtcNow;
        return result;
    }

    /// <summary>
    /// Updates an existing clue in a stage.
    /// RB-14: Blocked if mission is Active OR has sessions in progress.
    /// </summary>
    public Result<Clue> UpdateClue(
        Guid stageId,
        Guid clueId,
        bool hasActiveSessions,
        int order,
        string? content,
        double? latitude,
        double? longitude,
        double? radiusMeters)
    {
        if (Status == MissionStatus.Active || hasActiveSessions)
            return Result.Failure<Clue>(ClueErrors.StageLocked);

        var stage = _stages.FirstOrDefault(s => s.Id == stageId);
        if (stage is null)
            return Result.Failure<Clue>(StageErrors.NotFound);

        var result = stage.UpdateClue(clueId, order, content, latitude, longitude, radiusMeters);
        if (result.IsFailure)
            return result;

        UpdatedAt = DateTime.UtcNow;
        return result;
    }

    /// <summary>
    /// Removes a clue from a stage.
    /// RB-14: Blocked if mission is Active OR has sessions in progress.
    /// </summary>
    public Result<Clue> RemoveClue(Guid stageId, Guid clueId, bool hasActiveSessions)
    {
        if (Status == MissionStatus.Active || hasActiveSessions)
            return Result.Failure<Clue>(ClueErrors.StageLocked);

        var stage = _stages.FirstOrDefault(s => s.Id == stageId);
        if (stage is null)
            return Result.Failure<Clue>(StageErrors.NotFound);

        var result = stage.RemoveClue(clueId);
        if (result.IsFailure)
            return result;

        UpdatedAt = DateTime.UtcNow;
        return result;
    }
}
