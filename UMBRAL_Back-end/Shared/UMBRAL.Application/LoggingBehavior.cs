namespace UMBRAL.Application;

using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

/// <summary>
/// MediatR pipeline behavior shared by every UMBRAL backend service. Wraps every
/// command/query with structured start/end/failure logging and timing, replacing
/// the ad-hoc ILogger calls previously scattered across handlers.
///
/// Registered once per service via <c>cfg.AddOpenBehavior(typeof(LoggingBehavior&lt;,&gt;))</c>
/// inside the existing <c>AddMediatR</c> call — no per-handler wiring needed.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("→ {RequestName}", requestName);

        try
        {
            var response = await next();
            _logger.LogInformation(
                "← {RequestName} completado en {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (OperationCanceledException)
        {
            // Cancelación normal del cliente (timeout, navegación) — no es un error.
            _logger.LogInformation(
                "⊘ {RequestName} cancelado tras {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, "✗ {RequestName} falló tras {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
