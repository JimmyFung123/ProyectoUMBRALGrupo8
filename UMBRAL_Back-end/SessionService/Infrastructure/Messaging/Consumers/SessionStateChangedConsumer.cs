namespace SessionService.Infrastructure.Messaging.Consumers;

using MassTransit;
using SessionService.Application.Sessions;
using UMBRAL.Contracts.Events;

/// <summary>
/// Reacts to SessionStateChangedIntegrationEvent published by SessionService command
/// handlers. Relays the SignalR notification asynchronously so a transient hub
/// failure never blocks or fails the operator-facing command that already succeeded.
/// </summary>
public class SessionStateChangedConsumer : IConsumer<SessionStateChangedIntegrationEvent>
{
    private readonly ISessionNotifier _notifier;

    public SessionStateChangedConsumer(ISessionNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task Consume(ConsumeContext<SessionStateChangedIntegrationEvent> context)
    {
        var message = context.Message;
        return _notifier.NotifyStateChangedAsync(message.SessionId, message.NewStatus, context.CancellationToken);
    }
}
