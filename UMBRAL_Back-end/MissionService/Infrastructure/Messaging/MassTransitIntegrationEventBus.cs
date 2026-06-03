namespace UMBRAL_Back_end.Infrastructure.Messaging;

using MassTransit;
using UMBRAL_Back_end.Application;

public sealed class MassTransitIntegrationEventBus(IPublishEndpoint endpoint) : IIntegrationEventBus
{
    public Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
        => endpoint.Publish(message, ct);
}
