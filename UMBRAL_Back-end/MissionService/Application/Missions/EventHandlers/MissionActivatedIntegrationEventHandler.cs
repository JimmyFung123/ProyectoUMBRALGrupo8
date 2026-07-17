namespace UMBRAL_Back_end.Application.Missions.EventHandlers;

using MediatR;
using UMBRAL.Contracts.Events;
using UMBRAL_Back_end.Domain.Missions.Events;

public class MissionActivatedIntegrationEventHandler(IIntegrationEventBus bus)
    : INotificationHandler<MissionActivatedDomainEvent>
{
    public Task Handle(MissionActivatedDomainEvent e, CancellationToken ct) =>
        bus.PublishAsync(
            new MissionActivatedIntegrationEvent(e.MissionId, e.Name, e.OccurredAt, e.Difficulty.ToString()),
            ct);
}
