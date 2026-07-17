namespace UMBRAL_Back_end.Application.Missions.EventHandlers;

using MediatR;
using UMBRAL.Contracts.Events;
using UMBRAL_Back_end.Domain.Missions.Events;

public class MissionCreatedIntegrationEventHandler(IIntegrationEventBus bus)
    : INotificationHandler<MissionCreatedDomainEvent>
{
    public Task Handle(MissionCreatedDomainEvent e, CancellationToken ct) =>
        bus.PublishAsync(
            new MissionCreatedIntegrationEvent(e.MissionId, e.Name, e.Status.ToString(), e.CreatedAt),
            ct);
}
