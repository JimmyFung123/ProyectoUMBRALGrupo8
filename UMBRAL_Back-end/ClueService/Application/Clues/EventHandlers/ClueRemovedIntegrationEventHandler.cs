namespace ClueService.Application.Clues.EventHandlers;

using MediatR;
using ClueService.Domain.Clues.Events;
using UMBRAL.Contracts.Events;

public class ClueRemovedIntegrationEventHandler(IIntegrationEventBus bus)
    : INotificationHandler<ClueRemovedDomainEvent>
{
    public Task Handle(ClueRemovedDomainEvent e, CancellationToken ct) =>
        bus.PublishAsync(
            new ClueRemovedIntegrationEvent(e.ClueId, e.StageId, e.MissionId, e.OccurredAt),
            ct);
}
