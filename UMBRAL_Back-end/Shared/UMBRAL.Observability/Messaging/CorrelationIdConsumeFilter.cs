namespace UMBRAL.Observability.Messaging;

using MassTransit;

/// <summary>
/// Filtro de consumo de MassTransit que recupera el correlation id de la cabecera
/// del mensaje (o del CorrelationId nativo, o genera uno) y lo fija en
/// <see cref="CorrelationIdContext"/> mientras se procesa el mensaje. Así, todo
/// lo que dispare el consumidor (publicar nuevos eventos, llamadas HTTP, logs)
/// mantiene el mismo id que la operación HTTP que arrancó la cadena.
/// </summary>
public sealed class CorrelationIdConsumeFilter<T> : IFilter<ConsumeContext<T>>
    where T : class
{
    public void Probe(ProbeContext context) => context.CreateFilterScope("umbral-correlation-consume");

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var correlationId =
            context.Headers.Get<string>(CorrelationConstants.HeaderName)
            ?? context.CorrelationId?.ToString()
            ?? Guid.NewGuid().ToString();

        CorrelationIdContext.Current = correlationId;
        try
        {
            await next.Send(context);
        }
        finally
        {
            CorrelationIdContext.Current = null;
        }
    }
}
