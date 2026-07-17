namespace SessionService.Application.Sessions.EventHandlers;

using MediatR;
using SessionService.Application.Sessions.Commands.CreateSession;
using SessionService.Domain.Sessions;
using SessionService.Domain.Sessions.Events;
using UMBRAL.Contracts.Events;

public class SessionCreatedIntegrationEventHandler(IIntegrationEventBus bus)
    : INotificationHandler<SessionCreatedDomainEvent>
{
    public Task Handle(SessionCreatedDomainEvent e, CancellationToken ct) =>
        bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                e.SessionId,
                $"Se creó la sesión '{e.Name}'.",
                ActorName: e.OperatorName,
                CommandType: nameof(CreateSessionCommand),
                Outcome: SessionEvent.OutcomeSuccess,
                e.OccurredAt),
            ct);
}
