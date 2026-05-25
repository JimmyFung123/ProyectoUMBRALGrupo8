namespace SessionService.Domain.Sessions;

/// <summary>
/// An auditable event that occurred during a session's lifecycle (HU-9 / HU-22).
/// Created by the system (e.g. clue auto-released after a timer) or by operator
/// actions (clue released manually, team penalized, session paused, etc.).
/// Used to populate the operational dashboard log and the full audit timeline.
/// </summary>
public class SessionEvent
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }

    /// <summary>
    /// Human-readable name of who triggered the event — typically the Operator's
    /// nickname captured by the front-end via the X-Operator-Name header.
    /// "Sistema" for events emitted by background services (auto-released clues,
    /// auto-start, etc.). Required by HU-22 criterion 1.
    /// </summary>
    public string ActorName { get; private set; } = SystemActor;

    /// <summary>Sentinel used for events that were not triggered by a human operator.</summary>
    public const string SystemActor = "Sistema";

    private SessionEvent() { }

    public static SessionEvent Create(Guid sessionId, string description, string? actorName = null)
    {
        var actor = string.IsNullOrWhiteSpace(actorName) ? SystemActor : actorName.Trim();
        return new SessionEvent
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Description = description,
            OccurredAt = DateTime.UtcNow,
            ActorName = actor,
        };
    }
}
