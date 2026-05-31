namespace SessionService.Application.Sessions.Validation;

using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

/// <summary>Link 1 — rejects if the session was not found.</summary>
public class SessionExistsValidator : EvidenceValidatorBase
{
    public override Result Validate(EvidenceValidationContext context)
        => context.Session is null
            ? Result.Failure(SessionErrors.NotFound)
            : PassToNext(context);
}
