namespace SessionService.Application.Sessions.EventHandlers;

using MediatR;
using SessionService.Application.Sessions.Commands.ResumeSession;
using SessionService.Domain.Sessions;
using SessionService.Domain.Sessions.Events;
using UMBRAL.Contracts.Events;

public class SessionResumedIntegrationEventHandler(IIntegrationEventBus bus)
    : INotificationHandler<SessionResumedDomainEvent>
{
    public async Task Handle(SessionResumedDomainEvent e, CancellationToken ct)
    {
        await bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                e.SessionId,
                "La sesión fue reanudada.",
                ActorName: e.OperatorName,
                CommandType: nameof(ResumeSessionCommand),
                Outcome: SessionEvent.OutcomeSuccess,
                e.OccurredAt),
            ct);

        await bus.PublishAsync(
            new SessionStateChangedIntegrationEvent(e.SessionId, e.Status.ToString()),
            ct);
    }
}
