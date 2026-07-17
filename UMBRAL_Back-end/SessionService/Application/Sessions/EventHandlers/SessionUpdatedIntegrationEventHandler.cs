namespace SessionService.Application.Sessions.EventHandlers;

using MediatR;
using SessionService.Application.Sessions.Commands.UpdateSession;
using SessionService.Domain.Sessions;
using SessionService.Domain.Sessions.Events;
using UMBRAL.Contracts.Events;

public class SessionUpdatedIntegrationEventHandler(IIntegrationEventBus bus)
    : INotificationHandler<SessionUpdatedDomainEvent>
{
    public Task Handle(SessionUpdatedDomainEvent e, CancellationToken ct) =>
        bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                e.SessionId,
                $"Se editaron los datos de la sesión (nombre: '{e.Name}').",
                ActorName: e.OperatorName,
                CommandType: nameof(UpdateSessionCommand),
                Outcome: SessionEvent.OutcomeSuccess,
                e.OccurredAt),
            ct);
}
