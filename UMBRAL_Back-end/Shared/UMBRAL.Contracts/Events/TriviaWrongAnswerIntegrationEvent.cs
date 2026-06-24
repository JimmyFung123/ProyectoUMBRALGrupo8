namespace UMBRAL.Contracts.Events;

/// <summary>
/// Published when a team answers a trivia question incorrectly and still has
/// remaining attempts (i.e. the stage is NOT advanced).
/// TriviaWrongAnswerConsumer relays it to ISessionNotifier so all teammates
/// see which option was blocked and how many attempts remain.
/// </summary>
public record TriviaWrongAnswerIntegrationEvent(
    Guid SessionId,
    Guid TeamId,
    int StageOrder,
    Guid BlockedOptionId,
    int AttemptsUsed,
    int MaxAttempts,
    int ScoreChange,
    int NewScore,
    string? ParticipantName);
