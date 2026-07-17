namespace SessionService.Application.Sessions.EventHandlers;

using MediatR;
using SessionService.Application.Sessions.Commands.PauseSession;
using SessionService.Domain.Sessions;
using SessionService.Domain.Sessions.Events;
using UMBRAL.Contracts.Events;

public class SessionPausedIntegrationEventHandler(IIntegrationEventBus bus)
    : INotificationHandler<SessionPausedDomainEvent>
{
    public async Task Handle(SessionPausedDomainEvent e, CancellationToken ct)
    {
        await bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                e.SessionId,
                "La sesión fue pausada.",
                ActorName: e.OperatorName,
                CommandType: nameof(PauseSessionCommand),
                Outcome: SessionEvent.OutcomeSuccess,
                e.OccurredAt),
            ct);

        await bus.PublishAsync(
            new SessionStateChangedIntegrationEvent(e.SessionId, e.Status.ToString()),
            ct);
    }
}
