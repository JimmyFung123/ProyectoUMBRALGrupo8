namespace SessionService.Application.Sessions.EventHandlers;

using MediatR;
using SessionService.Application.Sessions.Commands.CancelSession;
using SessionService.Domain.Sessions;
using SessionService.Domain.Sessions.Events;
using UMBRAL.Contracts.Events;

public class SessionCancelledIntegrationEventHandler(IIntegrationEventBus bus)
    : INotificationHandler<SessionCancelledDomainEvent>
{
    public async Task Handle(SessionCancelledDomainEvent e, CancellationToken ct)
    {
        await bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                e.SessionId,
                "La sesión fue cancelada.",
                ActorName: e.OperatorName,
                CommandType: nameof(CancelSessionCommand),
                Outcome: SessionEvent.OutcomeSuccess,
                e.OccurredAt),
            ct);

        await bus.PublishAsync(
            new SessionCancelledIntegrationEvent(e.SessionId, e.OccurredAt),
            ct);
    }
}
