namespace SessionService.Application.Sessions.EventHandlers;

using MediatR;
using SessionService.Application.Sessions.Commands.StartSession;
using SessionService.Domain.Sessions;
using SessionService.Domain.Sessions.Events;
using UMBRAL.Contracts.Events;

public class SessionStartedIntegrationEventHandler(IIntegrationEventBus bus)
    : INotificationHandler<SessionStartedDomainEvent>
{
    public async Task Handle(SessionStartedDomainEvent e, CancellationToken ct)
    {
        await bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                e.SessionId,
                "La sesión fue iniciada.",
                ActorName: e.OperatorName,
                CommandType: nameof(StartSessionCommand),
                Outcome: SessionEvent.OutcomeSuccess,
                e.OccurredAt),
            ct);

        await bus.PublishAsync(
            new SessionStateChangedIntegrationEvent(e.SessionId, e.Status.ToString()),
            ct);
    }
}
