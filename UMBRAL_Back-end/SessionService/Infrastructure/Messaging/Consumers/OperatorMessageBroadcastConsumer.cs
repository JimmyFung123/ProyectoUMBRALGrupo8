namespace SessionService.Infrastructure.Messaging.Consumers;

using MassTransit;
using SessionService.Application.Sessions;
using UMBRAL.Contracts.Events;

/// <summary>
/// Reacts to OperatorMessageBroadcastIntegrationEvent (HU-28) published by
/// BroadcastOperatorMessageCommandHandler. Relays the SignalR notification
/// asynchronously so a transient hub failure never blocks or fails the
/// operator-facing command that already succeeded.
/// </summary>
public class OperatorMessageBroadcastConsumer : IConsumer<OperatorMessageBroadcastIntegrationEvent>
{
    private readonly ISessionNotifier _notifier;

    public OperatorMessageBroadcastConsumer(ISessionNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task Consume(ConsumeContext<OperatorMessageBroadcastIntegrationEvent> context)
    {
        var message = context.Message;
        return _notifier.NotifyOperatorMessageAsync(
            message.SessionId, message.Message, message.ActorName, message.DeliveredAt,
            context.CancellationToken);
    }
}
