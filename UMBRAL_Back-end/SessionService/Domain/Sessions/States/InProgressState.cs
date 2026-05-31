namespace SessionService.Domain.Sessions.States;

using SessionService.Domain.Common;

/// <summary>Activa — puede pausarse o finalizarse.</summary>
public class InProgressState : ISessionState
{
    public Result<bool> Start(Session context)
        => Result.Failure<bool>(SessionErrors.CannotStartSession);

    public Result<bool> Pause(Session context)
    {
        context.TransitionTo(SessionStatus.Paused);
        return Result.Success(true);
    }

    public Result<bool> Resume(Session context)
        => Result.Failure<bool>(SessionErrors.CannotResumeSession);

    public Result<bool> Finalize(Session context)
    {
        context.TransitionTo(SessionStatus.Completed);
        return Result.Success(true);
    }

    public Result<bool> Cancel(Session context)
        => Result.Failure<bool>(SessionErrors.CannotCancelNonPendingSession);

    public Result<bool> Update(Session context, string name, DateTime? scheduledAt)
        => Result.Failure<bool>(SessionErrors.CannotEditNonPendingSession);
}
