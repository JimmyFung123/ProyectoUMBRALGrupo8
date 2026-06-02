namespace SessionService.Infrastructure.Messaging;

using MassTransit;
using SessionService.Application;

public sealed class MassTransitIntegrationEventBus(IPublishEndpoint endpoint) : IIntegrationEventBus
{
    public Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
        => endpoint.Publish(message, ct);
}
