namespace SessionService.Domain.Sessions.States;

using SessionService.Domain.Common;

/// <summary>Finalizada — estado terminal, ninguna transición permitida (RB-17).</summary>
public class CompletedState : ISessionState
{
    public Result<bool> Start(Session context)
        => Result.Failure<bool>(SessionErrors.CannotStartSession);

    public Result<bool> Pause(Session context)
        => Result.Failure<bool>(SessionErrors.CannotPauseSession);

    public Result<bool> Resume(Session context)
        => Result.Failure<bool>(SessionErrors.CannotResumeSession);

    public Result<bool> Finalize(Session context)
        => Result.Failure<bool>(SessionErrors.CannotFinalizeSession);

    public Result<bool> Cancel(Session context)
        => Result.Failure<bool>(SessionErrors.CannotCancelNonPendingSession);

    public Result<bool> Update(Session context, string name, DateTime? scheduledAt)
        => Result.Failure<bool>(SessionErrors.CannotEditNonPendingSession);
}
