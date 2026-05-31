namespace SessionService.Application.Sessions.Validation;

using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

/// <summary>Link 3 — rejects if the stage was not found in StageService.</summary>
public class StageExistsValidator : EvidenceValidatorBase
{
    public override Result Validate(EvidenceValidationContext context)
        => context.Stage is null
            ? Result.Failure(SessionErrors.StageNotFound)
            : PassToNext(context);
}
