namespace SessionService.Domain.Sessions.States;

using SessionService.Domain.Common;

/// <summary>En preparación — puede iniciar, cancelar o editarse.</summary>
public class PendingState : ISessionState
{
    public Result<bool> Start(Session context)
    {
        context.TransitionTo(SessionStatus.InProgress);
        return Result.Success(true);
    }

    public Result<bool> Pause(Session context)
        => Result.Failure<bool>(SessionErrors.CannotPauseSession);

    public Result<bool> Resume(Session context)
        => Result.Failure<bool>(SessionErrors.CannotResumeSession);

    public Result<bool> Finalize(Session context)
        => Result.Failure<bool>(SessionErrors.CannotFinalizeSession);

    public Result<bool> Cancel(Session context)
    {
        context.TransitionTo(SessionStatus.Cancelled);
        return Result.Success(true);
    }

    public Result<bool> Update(Session context, string name, DateTime? scheduledAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<bool>(SessionErrors.InvalidName);

        context.ApplyUpdate(name.Trim(), scheduledAt);
        return Result.Success(true);
    }
}
