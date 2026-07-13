namespace UMBRAL.Observability;

using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using UMBRAL.Observability.Messaging;

/// <summary>
/// Punto único de cableado de la trazabilidad por correlación para todos los
/// servicios UMBRAL. Mantiene los <c>Program.cs</c> cortos y garantiza que cada
/// servicio propague el correlation id de forma idéntica en HTTP y en MassTransit.
/// </summary>
public static class UmbralCorrelationExtensions
{
    /// <summary>
    /// Registra el <see cref="CorrelationIdDelegatingHandler"/> y lo engancha a
    /// TODOS los HttpClient del contenedor (vía <c>ConfigureHttpClientDefaults</c>),
    /// de modo que cualquier llamada saliente reenvía el correlation id sin tener
    /// que tocar cada <c>AddHttpClient</c> uno por uno.
    /// </summary>
    public static IServiceCollection AddUmbralCorrelationId(this IServiceCollection services)
    {
        services.AddTransient<CorrelationIdDelegatingHandler>();
        services.ConfigureHttpClientDefaults(builder =>
            builder.AddHttpMessageHandler<CorrelationIdDelegatingHandler>());
        return services;
    }

    /// <summary>
    /// Coloca el <see cref="CorrelationIdMiddleware"/> al inicio del pipeline HTTP.
    /// Debe invocarse lo antes posible para que TODA la petición quede etiquetada.
    /// </summary>
    public static IApplicationBuilder UseUmbralCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();

    /// <summary>
    /// Registra los filtros de envío/publicación/consumo de MassTransit que
    /// propagan el correlation id como cabecera del mensaje. Invocar dentro de
    /// <c>UsingRabbitMq((ctx, cfg) =&gt; cfg.UseUmbralCorrelationId(ctx))</c>.
    /// </summary>
    public static void UseUmbralCorrelationId(
        this IBusFactoryConfigurator cfg, IBusRegistrationContext context)
    {
        cfg.UseSendFilter(typeof(CorrelationIdSendFilter<>), context);
        cfg.UsePublishFilter(typeof(CorrelationIdPublishFilter<>), context);
        cfg.UseConsumeFilter(typeof(CorrelationIdConsumeFilter<>), context);
    }
}
