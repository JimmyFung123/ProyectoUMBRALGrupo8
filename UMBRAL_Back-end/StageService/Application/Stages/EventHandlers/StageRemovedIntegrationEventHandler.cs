namespace StageService.Application.Stages.EventHandlers;

using MediatR;
using StageService.Domain.Stages.Events;
using UMBRAL.Contracts.Events;

public class StageRemovedIntegrationEventHandler(IIntegrationEventBus bus)
    : INotificationHandler<StageRemovedDomainEvent>
{
    public Task Handle(StageRemovedDomainEvent e, CancellationToken ct) =>
        bus.PublishAsync(
            new StageRemovedIntegrationEvent(e.StageId, e.MissionId, e.OccurredAt),
            ct);
}
