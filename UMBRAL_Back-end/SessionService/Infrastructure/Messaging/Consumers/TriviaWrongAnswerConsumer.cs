namespace SessionService.Infrastructure.Messaging.Consumers;

using MassTransit;
using SessionService.Application.Sessions;
using UMBRAL.Contracts.Events;

/// <summary>
/// Reacts to TriviaWrongAnswerIntegrationEvent published when a team answers
/// a trivia question incorrectly and still has remaining attempts.
/// Relays the SignalR notification asynchronously so all teammates see which
/// option was blocked and how many attempts remain.
/// </summary>
public class TriviaWrongAnswerConsumer : IConsumer<TriviaWrongAnswerIntegrationEvent>
{
    private readonly ISessionNotifier _notifier;

    public TriviaWrongAnswerConsumer(ISessionNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task Consume(ConsumeContext<TriviaWrongAnswerIntegrationEvent> context)
    {
        var m = context.Message;
        return _notifier.NotifyTriviaWrongAnswerAsync(
            m.SessionId, m.TeamId, m.StageOrder,
            m.BlockedOptionId, m.AttemptsUsed, m.MaxAttempts,
            m.ScoreChange, m.NewScore, m.ParticipantName,
            context.CancellationToken);
    }
}
