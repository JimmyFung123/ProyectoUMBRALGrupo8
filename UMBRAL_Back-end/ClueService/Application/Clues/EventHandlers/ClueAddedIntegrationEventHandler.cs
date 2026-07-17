namespace ClueService.Application.Clues.EventHandlers;

using MediatR;
using ClueService.Domain.Clues.Events;
using UMBRAL.Contracts.Events;

public class ClueAddedIntegrationEventHandler(IIntegrationEventBus bus)
    : INotificationHandler<ClueAddedDomainEvent>
{
    public Task Handle(ClueAddedDomainEvent e, CancellationToken ct) =>
        bus.PublishAsync(
            new ClueAddedIntegrationEvent(
                e.ClueId, e.StageId, e.MissionId, e.Content, e.Latitude, e.Longitude, e.RadiusMeters, e.OccurredAt),
            ct);
}
