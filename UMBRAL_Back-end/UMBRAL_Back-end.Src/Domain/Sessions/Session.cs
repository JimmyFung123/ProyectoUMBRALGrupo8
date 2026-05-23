namespace UMBRAL_Back_end.Domain.Sessions;

using UMBRAL_Back_end.Domain.Common;

public class Session
{
    public Guid Id { get; private set; }
    public Guid MissionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public SessionStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ScheduledAt { get; private set; }

    private Session() { }

    private Session(Guid id, Guid missionId, string name, DateTime? scheduledAt)
    {
        Id = id;
        MissionId = missionId;
        Name = name;
        Status = SessionStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        ScheduledAt = scheduledAt;
    }

    /// <summary>
    /// Creates a new session. Caller must verify that the mission exists and is Active.
    /// </summary>
    public static Result<Session> Create(Guid missionId, string name, DateTime? scheduledAt = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Session>(SessionErrors.InvalidName);

        if (missionId == Guid.Empty)
            return Result.Failure<Session>(SessionErrors.MissionRequired);

        return Result.Success(new Session(Guid.NewGuid(), missionId, name.Trim(), scheduledAt));
    }
}
