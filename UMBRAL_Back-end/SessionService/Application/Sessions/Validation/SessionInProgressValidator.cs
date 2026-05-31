namespace SessionService.Application.Sessions.Validation;

using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

/// <summary>Link 2 — rejects if the session is not actively in progress (RB-03).</summary>
public class SessionInProgressValidator : EvidenceValidatorBase
{
    public override Result Validate(EvidenceValidationContext context)
        => context.Session!.Status != SessionStatus.InProgress
            ? Result.Failure(SessionErrors.NotInProgress)
            : PassToNext(context);
}
