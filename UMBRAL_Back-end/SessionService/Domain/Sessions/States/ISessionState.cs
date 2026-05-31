namespace SessionService.Domain.Sessions.States;

using SessionService.Domain.Common;

/// <summary>
/// State pattern — each concrete state encapsulates the transitions that are
/// valid from that state and rejects the rest with the appropriate domain error.
/// The Session aggregate delegates all lifecycle calls to its CurrentState.
/// </summary>
public interface ISessionState
{
    Result<bool> Start(Session context);
    Result<bool> Pause(Session context);
    Result<bool> Resume(Session context);
    Result<bool> Finalize(Session context);
    Result<bool> Cancel(Session context);
    Result<bool> Update(Session context, string name, DateTime? scheduledAt);
}
