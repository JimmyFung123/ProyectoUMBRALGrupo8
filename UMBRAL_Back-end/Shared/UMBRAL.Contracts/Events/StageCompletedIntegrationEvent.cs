namespace UMBRAL.Contracts.Events;

/// <summary>
/// Published whenever a team completes a stage (Trivia answer or QR scan).
/// StageCompletedConsumer relays it to ISessionNotifier asynchronously.
/// </summary>
public record StageCompletedIntegrationEvent(
    Guid SessionId, Guid TeamId, int StageOrder, string StageType,
    bool WasCorrect, int PointsEarned, int NewScore, int NextStageOrder, bool IsLastStage);
