namespace SessionService.Domain.Sessions;

using SessionService.Domain.Common;

public class Session
{
    public Guid Id { get; private set; }
    public Guid MissionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public SessionStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ScheduledAt { get; private set; }

    private Session() { }

    public static Result<Session> Create(Guid missionId, string name, DateTime? scheduledAt = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Session>(SessionErrors.InvalidName);

        return Result.Success(new Session
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            Name = name.Trim(),
            Status = SessionStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ScheduledAt = scheduledAt
        });
    }

    /// <summary>
    /// Cancels the session. Only allowed while still Pending.
    /// Enrolled teams must be removed by the caller before persisting.
    /// </summary>
    public Result<bool> Cancel()
    {
        if (Status != SessionStatus.Pending)
            return Result.Failure<bool>(SessionErrors.CannotCancelNonPendingSession);

        Status = SessionStatus.Cancelled;
        return Result.Success(true);
    }

    /// <summary>
    /// Updates name and scheduled date. Only allowed when the session is still Pending.
    /// </summary>
    public Result<bool> Update(string name, DateTime? scheduledAt)
    {
        if (Status != SessionStatus.Pending)
            return Result.Failure<bool>(SessionErrors.CannotEditNonPendingSession);

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<bool>(SessionErrors.InvalidName);

        Name = name.Trim();
        ScheduledAt = scheduledAt;
        return Result.Success(true);
    }
}
