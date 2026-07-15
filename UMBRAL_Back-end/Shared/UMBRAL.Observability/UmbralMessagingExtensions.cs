namespace UMBRAL.Observability;

using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class UmbralMessagingExtensions
{
    public static IServiceCollection AddUmbralMassTransit(
        this IServiceCollection services,
        IConfiguration configuration,
        string queuePrefix,
        Action<IBusRegistrationConfigurator> configureConsumers)
    {
        var useInMemoryTransport = configuration.GetValue<bool>("Messaging:UseInMemoryTransport");

        services.AddMassTransit(x =>
        {
            configureConsumers(x);

            if (useInMemoryTransport)
            {
                x.UsingInMemory((ctx, cfg) =>
                {
                    cfg.UseUmbralCorrelationId(ctx);
                    cfg.ConfigureEndpoints(ctx, new KebabCaseEndpointNameFormatter(prefix: queuePrefix, includeNamespace: false));
                });
            }
            else
            {
                x.UsingRabbitMq((ctx, cfg) =>
                {
                    cfg.UseUmbralCorrelationId(ctx);
                    cfg.Host(new Uri(configuration.GetConnectionString("RabbitMQ")
                                     ?? "amqp://guest:guest@localhost:5672/"));
                    cfg.ConfigureEndpoints(ctx, new KebabCaseEndpointNameFormatter(prefix: queuePrefix, includeNamespace: false));
                });
            }
        });

        return services;
    }
}
