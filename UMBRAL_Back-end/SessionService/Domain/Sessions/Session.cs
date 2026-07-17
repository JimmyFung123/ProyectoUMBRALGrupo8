namespace SessionService.Domain.Sessions;

using SessionService.Domain.Common;
using SessionService.Domain.Sessions.States;

public class Session
{
    public Guid Id { get; private set; }
    public Guid MissionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public SessionStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ScheduledAt { get; private set; }

    /// <summary>Short alphanumeric code participants use to find this session (e.g. "ABC123").</summary>
    public string AccessCode { get; private set; } = string.Empty;

    /// <summary>Keycloak <c>sub</c> of the operator who created this session (RB-10). Null for sessions created before this field existed.</summary>
    public string? CreatedByOperatorId { get; private set; }

    private Session() { }

    // State pattern — current state is derived from the persisted Status enum so
    // EF Core never needs to store it. A new object is resolved on each call;
    // states are stateless and cheap to construct.
    private ISessionState CurrentState => Status switch
    {
        SessionStatus.Pending    => new PendingState(),
        SessionStatus.InProgress => new InProgressState(),
        SessionStatus.Paused     => new PausedState(),
        SessionStatus.Completed  => new CompletedState(),
        SessionStatus.Cancelled  => new CancelledState(),
        _                        => new PendingState()
    };

    public static Result<Session> Create(
        Guid missionId, string name, DateTime? scheduledAt = null, string? createdByOperatorId = null)
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
            ScheduledAt = scheduledAt,
            AccessCode = SessionCode.Generate().Value,
            CreatedByOperatorId = createdByOperatorId,
        });
    }

    // ── Public lifecycle API (delegates to current state) ────────────────────

    /// <summary>Starts the session. Only allowed while Pending.</summary>
    public Result<bool> Start()    => CurrentState.Start(this);

    /// <summary>Pauses the session. Only allowed while InProgress.</summary>
    public Result<bool> Pause()    => CurrentState.Pause(this);

    /// <summary>Resumes the session. Only allowed while Paused.</summary>
    public Result<bool> Resume()   => CurrentState.Resume(this);

    /// <summary>Finalizes the session. Allowed from InProgress or Paused. Irreversible.</summary>
    public Result<bool> Finalize() => CurrentState.Finalize(this);

    /// <summary>
    /// Cancels the session. Only allowed while still Pending.
    /// Enrolled teams must be removed by the caller before persisting.
    /// </summary>
    public Result<bool> Cancel()   => CurrentState.Cancel(this);

    /// <summary>Updates name and scheduled date. Only allowed when still Pending.</summary>
    public Result<bool> Update(string name, DateTime? scheduledAt)
        => CurrentState.Update(this, name, scheduledAt);

    // ── Internal hooks called by state objects ────────────────────────────────

    /// <summary>Called by concrete states to apply a status transition.</summary>
    internal void TransitionTo(SessionStatus newStatus) => Status = newStatus;

    /// <summary>Called by PendingState to apply property changes on Update.</summary>
    internal void ApplyUpdate(string name, DateTime? scheduledAt)
    {
        Name = name;
        ScheduledAt = scheduledAt;
    }
}
