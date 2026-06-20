namespace SessionService.Infrastructure.Messaging.Consumers;

using MassTransit;
using SessionService.Application.Sessions;
using UMBRAL.Contracts.Events;

/// <summary>
/// Reacts to ClueReleasedIntegrationEvent published whenever a clue is released
/// to a team (manual or automatic). Relays the SignalR notification
/// asynchronously so a transient hub failure never blocks or fails the
/// command/background job that already succeeded.
/// </summary>
public class ClueReleasedConsumer : IConsumer<ClueReleasedIntegrationEvent>
{
    private readonly ISessionNotifier _notifier;

    public ClueReleasedConsumer(ISessionNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task Consume(ConsumeContext<ClueReleasedIntegrationEvent> context)
    {
        var message = context.Message;
        return _notifier.NotifyClueReleasedAsync(
            message.SessionId, message.TeamId, message.Content,
            message.Latitude, message.Longitude, message.RadiusMeters,
            message.ClueNumber, message.IsAutomatic,
            context.CancellationToken);
    }
}
