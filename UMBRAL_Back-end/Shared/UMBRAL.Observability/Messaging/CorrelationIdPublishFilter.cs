namespace UMBRAL.Observability.Messaging;

using MassTransit;

/// <summary>
/// Filtro de publicación de MassTransit que fija el correlation id en la cabecera
/// del evento publicado. Se registra junto al de envío para asegurar que el id
/// viaja aunque una versión del broker/topología no encadene ambos pipes.
/// </summary>
public sealed class CorrelationIdPublishFilter<T> : IFilter<PublishContext<T>>
    where T : class
{
    public void Probe(ProbeContext context) => context.CreateFilterScope("umbral-correlation-publish");

    public Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        var correlationId = CorrelationIdContext.Current;
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            context.Headers.Set(CorrelationConstants.HeaderName, correlationId);

            if (context.CorrelationId is null && Guid.TryParse(correlationId, out var guid))
                context.CorrelationId = guid;
        }

        return next.Send(context);
    }
}
