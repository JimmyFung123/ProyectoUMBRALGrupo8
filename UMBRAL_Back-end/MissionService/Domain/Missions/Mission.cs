namespace UMBRAL_Back_end.Domain.Missions;

using UMBRAL_Back_end.Domain.Common;

/// <summary>
/// Aggregate Root of the Mission Design context (v2). Owns mission metadata and
/// lifecycle (Inactive ↔ Active). Stages and clues are NOT part of this aggregate —
/// they are owned by StageService / ClueService and referenced by id. The number
/// of stages is mirrored locally via <see cref="StageCountLookup"/> to enforce
/// RB-13 (a mission needs ≥1 stage to activate) without a synchronous call.
///
/// Input validation is delegated to the value objects <see cref="MissionName"/>,
/// <see cref="MissionDescription"/> and <see cref="SessionDuration"/>. The public
/// properties stay primitive so EF Core mapping, DTOs and the frontend contract
/// remain unchanged.
/// </summary>
public class Mission : AggregateRoot
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DifficultyLevel Difficulty { get; private set; }

    /// <summary>Maximum duration in minutes (the diagram's SessionDuration VO).</summary>
    public int MaxDuration { get; private set; }
    public MissionStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Mission() { }

    private Mission(Guid id, MissionName name, MissionDescription description, DifficultyLevel difficulty, SessionDuration duration)
    {
        Id = id;
        Name = name.Value;
        Description = description.Value;
        Difficulty = difficulty;
        MaxDuration = duration.Minutes;
        Status = MissionStatus.Inactive;
        CreatedAt = DateTime.UtcNow;
    }

    public static Result<Mission> Create(string name, string description, DifficultyLevel difficulty, int maxDuration)
    {
        var nameResult = MissionName.Create(name);
        if (nameResult.IsFailure)
            return Result.Failure<Mission>(nameResult.Error);

        var descriptionResult = MissionDescription.Create(description);
        if (descriptionResult.IsFailure)
            return Result.Failure<Mission>(descriptionResult.Error);

        var durationResult = SessionDuration.Create(maxDuration);
        if (durationResult.IsFailure)
            return Result.Failure<Mission>(durationResult.Error);

        return Result.Success(new Mission(
            Guid.NewGuid(), nameResult.Value, descriptionResult.Value, difficulty, durationResult.Value));
    }

    /// <summary>
    /// Transitions to Active.
    /// Stage validation (RB-13) is enforced by the handler via StageCountLookup.
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
        var nameResult = MissionName.Create(name);
        if (nameResult.IsFailure)
            return nameResult;

        var descriptionResult = MissionDescription.Create(description);
        if (descriptionResult.IsFailure)
            return descriptionResult;

        var durationResult = SessionDuration.Create(maxDuration);
        if (durationResult.IsFailure)
            return durationResult;

        if (hasActiveSessions)
            return Result.Failure(MissionErrors.HasActiveSessions);

        Name = nameResult.Value.Value;
        Description = descriptionResult.Value.Value;
        Difficulty = difficulty;
        MaxDuration = durationResult.Value.Minutes;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }
}
