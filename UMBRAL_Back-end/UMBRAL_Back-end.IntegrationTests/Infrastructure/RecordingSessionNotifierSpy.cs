// SessionService generates its own top-level-statements `Program` class in the *global*
// namespace, same conflict handled elsewhere in this project (see
// SessionServiceRabbitMqApiFactory.cs). Needed here directly because this spy implements
// SessionService's own ISessionNotifier interface.
extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using System.Collections.Concurrent;
using SessionServiceAssembly::SessionService.Application.Sessions;

/// <summary>
/// Test double for <see cref="ISessionNotifier"/> registered in place of the real SignalR
/// implementation for the six Level-2 "pure relay" consumers (see
/// <c>SessionNotifierRelayFixture</c>): those consumers only forward an integration event
/// onto SignalR, they never touch a DbContext, so DB polling is not an option for asserting
/// they ran. This spy records every call instead so the test can poll its queues.
///
/// MassTransit dispatches consumers concurrently, so every method appends to its own
/// <see cref="ConcurrentQueue{T}"/> rather than a shared mutable list.
/// </summary>
public class RecordingSessionNotifierSpy : ISessionNotifier
{
    public record StateChangedCall(Guid SessionId, string? NewStatus);

    public record OperatorMessageCall(Guid SessionId, string Message, string ActorName, DateTime DeliveredAt);

    public record StageCompletedCall(
        Guid SessionId, Guid TeamId, int StageOrder, string StageType,
        bool WasCorrect, int PointsEarned, int NewScore, int NextStageOrder, bool IsLastStage);

    public record ClueReleasedCall(
        Guid SessionId, Guid TeamId, string? Content,
        double? Latitude, double? Longitude, int? RadiusMeters,
        int ClueNumber, bool IsAutomatic);

    public record TeamPenalizedCall(
        Guid SessionId, Guid TeamId, string TeamName,
        int Points, string Reason, int NewScore, string ActorName);

    public record TriviaWrongAnswerCall(
        Guid SessionId, Guid TeamId, int StageOrder,
        Guid BlockedOptionId, int AttemptsUsed, int MaxAttempts,
        int ScoreChange, int NewScore, string? ParticipantName);

    public ConcurrentQueue<StateChangedCall> StateChangedCalls { get; } = new();
    public ConcurrentQueue<OperatorMessageCall> OperatorMessageCalls { get; } = new();
    public ConcurrentQueue<StageCompletedCall> StageCompletedCalls { get; } = new();
    public ConcurrentQueue<ClueReleasedCall> ClueReleasedCalls { get; } = new();
    public ConcurrentQueue<TeamPenalizedCall> TeamPenalizedCalls { get; } = new();
    public ConcurrentQueue<TriviaWrongAnswerCall> TriviaWrongAnswerCalls { get; } = new();

    public Task NotifyStateChangedAsync(Guid sessionId, string? newStatus = null, CancellationToken ct = default)
    {
        StateChangedCalls.Enqueue(new StateChangedCall(sessionId, newStatus));
        return Task.CompletedTask;
    }

    public Task NotifyOperatorMessageAsync(
        Guid sessionId, string message, string actorName, DateTime deliveredAt,
        CancellationToken ct = default)
    {
        OperatorMessageCalls.Enqueue(new OperatorMessageCall(sessionId, message, actorName, deliveredAt));
        return Task.CompletedTask;
    }

    public Task NotifyStageCompletedAsync(
        Guid sessionId, Guid teamId, int stageOrder, string stageType,
        bool wasCorrect, int pointsEarned, int newScore, int nextStageOrder, bool isLastStage,
        CancellationToken ct = default)
    {
        StageCompletedCalls.Enqueue(new StageCompletedCall(
            sessionId, teamId, stageOrder, stageType, wasCorrect, pointsEarned, newScore, nextStageOrder, isLastStage));
        return Task.CompletedTask;
    }

    public Task NotifyClueReleasedAsync(
        Guid sessionId, Guid teamId, string? content,
        double? latitude, double? longitude, int? radiusMeters,
        int clueNumber, bool isAutomatic,
        CancellationToken ct = default)
    {
        ClueReleasedCalls.Enqueue(new ClueReleasedCall(
            sessionId, teamId, content, latitude, longitude, radiusMeters, clueNumber, isAutomatic));
        return Task.CompletedTask;
    }

    public Task NotifyTeamPenalizedAsync(
        Guid sessionId, Guid teamId, string teamName,
        int points, string reason, int newScore, string actorName,
        CancellationToken ct = default)
    {
        TeamPenalizedCalls.Enqueue(new TeamPenalizedCall(sessionId, teamId, teamName, points, reason, newScore, actorName));
        return Task.CompletedTask;
    }

    public Task NotifyTriviaWrongAnswerAsync(
        Guid sessionId, Guid teamId, int stageOrder,
        Guid blockedOptionId, int attemptsUsed, int maxAttempts,
        int scoreChange, int newScore, string? participantName,
        CancellationToken ct = default)
    {
        TriviaWrongAnswerCalls.Enqueue(new TriviaWrongAnswerCall(
            sessionId, teamId, stageOrder, blockedOptionId, attemptsUsed, maxAttempts, scoreChange, newScore, participantName));
        return Task.CompletedTask;
    }
}
