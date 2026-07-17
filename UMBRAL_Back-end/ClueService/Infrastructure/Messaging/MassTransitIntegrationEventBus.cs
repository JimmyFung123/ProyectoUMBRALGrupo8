namespace ClueService.Infrastructure.Messaging;

using ClueService.Application;
using MassTransit;

public sealed class MassTransitIntegrationEventBus(IPublishEndpoint endpoint) : IIntegrationEventBus
{
    public Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
        => endpoint.Publish(message, ct);
}
