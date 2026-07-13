namespace UMBRAL.Observability.Messaging;

using MassTransit;

/// <summary>
/// Filtro de envío de MassTransit que copia el correlation id ambiental a la
/// cabecera del mensaje saliente. Cubre tanto los envíos directos (Send) como
/// las publicaciones (el pipe de publish también atraviesa el de send), de modo
/// que todo evento de integración lleva el id de la operación que lo originó.
/// </summary>
public sealed class CorrelationIdSendFilter<T> : IFilter<SendContext<T>>
    where T : class
{
    public void Probe(ProbeContext context) => context.CreateFilterScope("umbral-correlation-send");

    public Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        var correlationId = CorrelationIdContext.Current;
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            context.Headers.Set(CorrelationConstants.HeaderName, correlationId);

            // Aprovecha el CorrelationId nativo de MassTransit si el id es un GUID.
            if (context.CorrelationId is null && Guid.TryParse(correlationId, out var guid))
                context.CorrelationId = guid;
        }

        return next.Send(context);
    }
}
