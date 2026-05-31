namespace SessionService.Application.Sessions.Validation;

using SessionService.Domain.Common;

/// <summary>
/// Chain of Responsibility pattern — abstract base for evidence validators.
/// Each concrete validator either rejects the request with a domain error or
/// passes it to the next validator in the chain via <see cref="Next"/>.
/// </summary>
public abstract class EvidenceValidatorBase
{
    private EvidenceValidatorBase? _next;

    /// <summary>
    /// Links the next validator in the chain and returns it so calls can be
    /// chained: <c>first.SetNext(second).SetNext(third)</c>.
    /// </summary>
    public EvidenceValidatorBase SetNext(EvidenceValidatorBase next)
    {
        _next = next;
        return next;
    }

    /// <summary>
    /// Validates the context. If the check passes, delegates to the next
    /// validator. If no next validator exists, returns success.
    /// </summary>
    public abstract Result Validate(EvidenceValidationContext context);

    /// <summary>Helper to pass the request to the next link, or succeed if none.</summary>
    protected Result PassToNext(EvidenceValidationContext context)
        => _next?.Validate(context) ?? Result.Success();
}
