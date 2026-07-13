namespace UMBRAL.Observability;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Middleware de borde para la trazabilidad por correlación. En cada petición:
///   * toma el <c>X-Correlation-ID</c> entrante o genera uno nuevo si no viene,
///   * lo fija en <see cref="CorrelationIdContext"/> (para que lo hereden las
///     llamadas HTTP salientes y los mensajes publicados durante la petición),
///   * normaliza la cabecera en el request (así YARP lo reenvía aguas abajo
///     cuando es el gateway quien lo genera),
///   * lo devuelve en la respuesta para que el cliente pueda correlacionar, y
///   * abre un scope de logging con el id, de modo que todos los logs de la
///     petición quedan etiquetados con él.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveIncoming(context) ?? Guid.NewGuid().ToString();

        CorrelationIdContext.Current = correlationId;

        // Normaliza el request para que quede disponible aguas abajo (p.ej. YARP
        // reenvía las cabeceras del request al servicio destino).
        context.Request.Headers[CorrelationConstants.HeaderName] = correlationId;

        // Al ser el primer middleware, la respuesta aún no empezó: se puede devolver
        // el id de forma segura y directa para que el cliente pueda correlacionar.
        context.Response.Headers[CorrelationConstants.HeaderName] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   [CorrelationConstants.LogPropertyName] = correlationId,
               }))
        {
            try
            {
                await _next(context);
            }
            finally
            {
                CorrelationIdContext.Current = null;
            }
        }
    }

    private static string? ResolveIncoming(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationConstants.HeaderName, out var values))
        {
            var incoming = values.ToString();
            if (!string.IsNullOrWhiteSpace(incoming))
                return incoming;
        }
        return null;
    }
}
