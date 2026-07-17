namespace UMBRAL.Observability.Messaging;

using MassTransit;

/// <summary>
/// Lógica compartida por los filtros de envío y publicación: copia el correlation
/// id ambiental a la cabecera del mensaje saliente. <see cref="PublishContext{T}"/>
/// deriva de <see cref="SendContext{T}"/>, así que ambos filtros reutilizan esto y
/// evitan duplicar la regla (DRY).
/// </summary>
internal static class CorrelationHeaderWriter
{
    public static void Apply(SendContext context)
    {
        var correlationId = CorrelationIdContext.Current;
        if (string.IsNullOrWhiteSpace(correlationId))
            return;

        context.Headers.Set(CorrelationConstants.HeaderName, correlationId);

        // Aprovecha el CorrelationId nativo de MassTransit si el id es un GUID.
        if (context.CorrelationId is null && Guid.TryParse(correlationId, out var guid))
            context.CorrelationId = guid;
    }
}
