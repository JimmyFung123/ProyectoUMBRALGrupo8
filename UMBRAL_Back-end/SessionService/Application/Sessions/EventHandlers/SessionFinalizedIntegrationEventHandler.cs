namespace SessionService.Application.Sessions.EventHandlers;

using MediatR;
using SessionService.Application.Sessions.Commands.FinalizeSession;
using SessionService.Domain.Sessions;
using SessionService.Domain.Sessions.Events;
using UMBRAL.Contracts.Events;

public class SessionFinalizedIntegrationEventHandler(IIntegrationEventBus bus)
    : INotificationHandler<SessionFinalizedDomainEvent>
{
    public async Task Handle(SessionFinalizedDomainEvent e, CancellationToken ct)
    {
        await bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                e.SessionId,
                "La sesión fue finalizada. Ranking definitivo calculado.",
                ActorName: e.OperatorName,
                CommandType: nameof(FinalizeSessionCommand),
                Outcome: SessionEvent.OutcomeSuccess,
                e.OccurredAt),
            ct);

        await bus.PublishAsync(
            new SessionStateChangedIntegrationEvent(e.SessionId, e.Status.ToString()),
            ct);
    }
}
