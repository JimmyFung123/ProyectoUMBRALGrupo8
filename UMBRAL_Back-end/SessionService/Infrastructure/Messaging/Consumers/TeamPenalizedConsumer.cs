namespace SessionService.Infrastructure.Messaging.Consumers;

using MassTransit;
using SessionService.Application.Sessions;
using UMBRAL.Contracts.Events;

/// <summary>
/// Reacts to TeamPenalizedIntegrationEvent published whenever an operator
/// penalizes a team. Relays the SignalR notification asynchronously so a
/// transient hub failure never blocks or fails the operator-facing command
/// that already succeeded.
/// </summary>
public class TeamPenalizedConsumer : IConsumer<TeamPenalizedIntegrationEvent>
{
    private readonly ISessionNotifier _notifier;

    public TeamPenalizedConsumer(ISessionNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task Consume(ConsumeContext<TeamPenalizedIntegrationEvent> context)
    {
        var message = context.Message;
        return _notifier.NotifyTeamPenalizedAsync(
            message.SessionId, message.TeamId, message.TeamName,
            message.Points, message.Reason, message.NewScore, message.ActorName,
            context.CancellationToken);
    }
}
