namespace SessionService.Infrastructure.Messaging.Consumers;

using MassTransit;
using SessionService.Domain.Sessions;
using UMBRAL.Contracts.Events;

/// <summary>
/// Reacts to SessionAuditIntegrationEvent published by SessionService command handlers.
/// Persists the SessionEvent asynchronously so a transient failure writing the audit
/// trail never blocks or fails the operator-facing command that already succeeded.
/// </summary>
public class SessionAuditConsumer : IConsumer<SessionAuditIntegrationEvent>
{
    private readonly ISessionEventRepository _eventRepository;

    public SessionAuditConsumer(ISessionEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public async Task Consume(ConsumeContext<SessionAuditIntegrationEvent> context)
    {
        var message = context.Message;
        var auditEvent = SessionEvent.Create(
            message.SessionId,
            message.Description,
            message.ActorName,
            message.CommandType,
            message.Outcome);

        await _eventRepository.AddAsync(auditEvent, context.CancellationToken);
        await _eventRepository.SaveChangesAsync(context.CancellationToken);
    }
}
