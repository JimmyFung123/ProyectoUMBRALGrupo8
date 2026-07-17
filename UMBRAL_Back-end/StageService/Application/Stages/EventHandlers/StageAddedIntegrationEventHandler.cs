namespace StageService.Application.Stages.EventHandlers;

using MediatR;
using StageService.Domain.Stages.Events;
using UMBRAL.Contracts.Events;

public class StageAddedIntegrationEventHandler(IIntegrationEventBus bus)
    : INotificationHandler<StageAddedDomainEvent>
{
    public Task Handle(StageAddedDomainEvent e, CancellationToken ct) =>
        bus.PublishAsync(
            new StageAddedIntegrationEvent(e.StageId, e.MissionId, e.Type.ToString(), e.OccurredAt),
            ct);
}
