namespace UMBRAL_Back_end.Domain.Missions;

using UMBRAL_Back_end.Domain.Common;

public class Mission
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DifficultyLevel Difficulty { get; private set; }
    public int MaxDuration { get; private set; }
    public MissionStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

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
    /// Transitions to Active.
    /// Stage validation is the responsibility of StageService.
    /// </summary>
    public Result Activate()
    {
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
}
