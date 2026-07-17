namespace SessionService.Application.Sessions;

public interface ISessionNotifier
{
    Task NotifyStateChangedAsync(Guid sessionId, string? newStatus = null, CancellationToken ct = default);

    Task NotifyOperatorMessageAsync(
        Guid sessionId, string message, string actorName, DateTime deliveredAt,
        CancellationToken ct = default);

    Task NotifyStageCompletedAsync(
        Guid sessionId, Guid teamId, int stageOrder, string stageType,
        bool wasCorrect, int pointsEarned, int newScore, int nextStageOrder, bool isLastStage,
        CancellationToken ct = default);

    Task NotifyClueReleasedAsync(
        Guid sessionId, Guid teamId, string? content,
        double? latitude, double? longitude, int? radiusMeters,
        int clueNumber, bool isAutomatic,
        CancellationToken ct = default);

    Task NotifyTeamPenalizedAsync(
        Guid sessionId, Guid teamId, string teamName,
        int points, string reason, int newScore, string actorName,
        CancellationToken ct = default);

    Task NotifyTriviaWrongAnswerAsync(
        Guid sessionId, Guid teamId, int stageOrder,
        Guid blockedOptionId, int attemptsUsed, int maxAttempts,
        int scoreChange, int newScore, string? participantName,
        CancellationToken ct = default);
}
