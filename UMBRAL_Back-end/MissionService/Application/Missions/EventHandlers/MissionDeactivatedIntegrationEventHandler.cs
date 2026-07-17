namespace UMBRAL_Back_end.Application.Missions.EventHandlers;

using MediatR;
using UMBRAL.Contracts.Events;
using UMBRAL_Back_end.Domain.Missions.Events;

public class MissionDeactivatedIntegrationEventHandler(IIntegrationEventBus bus)
    : INotificationHandler<MissionDeactivatedDomainEvent>
{
    public Task Handle(MissionDeactivatedDomainEvent e, CancellationToken ct) =>
        bus.PublishAsync(
            new MissionDeactivatedIntegrationEvent(e.MissionId, e.Name, e.OccurredAt),
            ct);
}
