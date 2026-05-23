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

    // EF Core uses the _stages backing field by convention (PropertyAccessMode.Field)
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
    /// Transitions the mission to Active. Fails if no stages are configured.
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
    /// Transitions the mission to Inactive. Fails if active sessions exist.
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
    /// Updates mission data. Fails if active sessions exist (immutability constraint).
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
}
