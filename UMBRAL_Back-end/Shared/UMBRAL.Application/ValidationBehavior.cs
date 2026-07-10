namespace UMBRAL.Application;

using FluentValidation;
using MediatR;

/// <summary>
/// MediatR pipeline behavior shared by every UMBRAL backend service. Runs every
/// registered FluentValidation <see cref="IValidator{T}"/> for the incoming
/// command/query BEFORE the handler (and any repository call it makes) executes.
///
/// A no-op when no validator is registered for TRequest — services don't need
/// a validator per command, only the ones where fail-fast input checking is
/// worth the extra class. Registered once per service via
/// <c>cfg.AddOpenBehavior(typeof(ValidationBehavior&lt;,&gt;))</c>, after
/// <see cref="LoggingBehavior{TRequest,TResponse}"/> so a validation failure is
/// still captured by the logging wrapper.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next();
    }
}
