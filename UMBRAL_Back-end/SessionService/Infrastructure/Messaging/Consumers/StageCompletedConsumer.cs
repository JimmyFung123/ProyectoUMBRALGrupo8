namespace SessionService.Infrastructure.Messaging.Consumers;

using MassTransit;
using SessionService.Application.Sessions;
using UMBRAL.Contracts.Events;

/// <summary>
/// Reacts to StageCompletedIntegrationEvent published when a team completes a
/// stage (Trivia answer or QR scan). Relays the SignalR notification
/// asynchronously so a transient hub failure never blocks or fails the
/// gameplay command that already succeeded.
/// </summary>
public class StageCompletedConsumer : IConsumer<StageCompletedIntegrationEvent>
{
    private readonly ISessionNotifier _notifier;

    public StageCompletedConsumer(ISessionNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task Consume(ConsumeContext<StageCompletedIntegrationEvent> context)
    {
        var message = context.Message;
        return _notifier.NotifyStageCompletedAsync(
            message.SessionId, message.TeamId, message.StageOrder, message.StageType,
            message.WasCorrect, message.PointsEarned, message.NewScore,
            message.NextStageOrder, message.IsLastStage,
            context.CancellationToken);
    }
}
