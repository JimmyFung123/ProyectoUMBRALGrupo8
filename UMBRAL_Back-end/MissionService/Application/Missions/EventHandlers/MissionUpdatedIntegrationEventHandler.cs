namespace UMBRAL_Back_end.Application.Missions.EventHandlers;

using MediatR;
using UMBRAL.Contracts.Events;
using UMBRAL_Back_end.Domain.Missions.Events;

public class MissionUpdatedIntegrationEventHandler(IIntegrationEventBus bus)
    : INotificationHandler<MissionUpdatedDomainEvent>
{
    public Task Handle(MissionUpdatedDomainEvent e, CancellationToken ct) =>
        bus.PublishAsync(
            new MissionUpdatedIntegrationEvent(e.MissionId, e.Name, e.Difficulty.ToString(), e.OccurredAt),
            ct);
}
