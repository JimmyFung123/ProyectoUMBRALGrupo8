namespace SessionService.Domain.Sessions;

/// <summary>
/// An auditable event that occurred during a session's lifecycle (HU-9 / HU-22 / HU-26).
/// Created by the system (e.g. clue auto-released after a timer) or by operator
/// actions (clue released manually, team penalized, session paused, etc.).
///
/// Powers two read views:
///   * HU-22 — human-readable timeline ("clue released by operator X").
///   * HU-26 — technical command audit log with <see cref="CommandType"/> +
///     <see cref="Outcome"/> + millisecond-precise <see cref="OccurredAt"/>.
///
/// Inmutability (HU-26 criterion 2) is enforced at two levels:
///   * Setters are private — only <see cref="Create"/> can build an instance.
///   * A <c>SaveChangesInterceptor</c> in the infrastructure layer rejects
///     EF Core <c>Modified</c>/<c>Deleted</c> states for this entity.
/// </summary>
public class SessionEvent
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }

    /// <summary>
    /// Human-readable name of who triggered the event — typically the Operator's
    /// display name extracted from the Keycloak JWT (HU-23). "Sistema" for events
    /// emitted by background services (auto-released clues, auto-start, etc.).
    /// Required by HU-22 criterion 1 and HU-26 criterion 1.
    /// </summary>
    public string ActorName { get; private set; } = SystemActor;

    /// <summary>
    /// Technical name of the CQRS command that produced this event (HU-26).
    /// Examples: "CreateSession", "PauseSession", "ReleaseClue", "PenalizeTeam".
    /// Nullable for events recorded before HU-26 introduced the column.
    /// </summary>
    public string? CommandType { get; private set; }

    /// <summary>
    /// Whether the recorded command succeeded or failed (HU-26). Only commands
    /// that opt to audit their failure path populate this (e.g. ValidateQrCode
    /// records a "Failure" event when the wrong QR is scanned). Nullable for
    /// legacy rows.
    /// </summary>
    public string? Outcome { get; private set; }

    /// <summary>Sentinel used for events that were not triggered by a human operator.</summary>
    public const string SystemActor = "Sistema";

    public const string OutcomeSuccess = "Success";
    public const string OutcomeFailure = "Failure";

    private SessionEvent() { }

    public static SessionEvent Create(
        Guid sessionId,
        string description,
        string? actorName = null,
        string? commandType = null,
        string? outcome = null)
    {
        var actor = ActorNameResolver.Resolve(actorName);
        return new SessionEvent
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Description = description,
            OccurredAt = DateTime.UtcNow,
            ActorName = actor,
            CommandType = string.IsNullOrWhiteSpace(commandType) ? null : commandType.Trim(),
            Outcome = string.IsNullOrWhiteSpace(outcome) ? null : outcome.Trim(),
        };
    }
}
