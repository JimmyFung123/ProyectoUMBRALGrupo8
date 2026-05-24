namespace SessionService.Domain.Sessions;

/// <summary>
/// An auditable event that occurred during a session's lifecycle.
/// Created by the system (e.g. session started) or by operator actions
/// (e.g. clue released, team advanced). Used to populate the operational dashboard log.
/// </summary>
public class SessionEvent
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }

    private SessionEvent() { }

    public static SessionEvent Create(Guid sessionId, string description)
    {
        return new SessionEvent
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Description = description,
            OccurredAt = DateTime.UtcNow,
        };
    }
}
