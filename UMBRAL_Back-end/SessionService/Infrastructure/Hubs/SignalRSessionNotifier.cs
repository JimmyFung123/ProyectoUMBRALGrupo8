namespace SessionService.Infrastructure.Hubs;

using Microsoft.AspNetCore.SignalR;
using SessionService.Application.Sessions;

public sealed class SignalRSessionNotifier(IHubContext<SessionHub> hub) : ISessionNotifier
{
    public Task NotifyStateChangedAsync(Guid sessionId, string? newStatus = null, CancellationToken ct = default)
    {
        var clients = hub.Clients.Group(sessionId.ToString());
        return newStatus is not null
            ? clients.SendAsync("SessionStateChanged", new { SessionId = sessionId, NewStatus = newStatus }, ct)
            : clients.SendAsync("SessionStateChanged", ct);
    }

    public async Task NotifyOperatorMessageAsync(
        Guid sessionId, string message, string actorName, DateTime deliveredAt,
        CancellationToken ct = default)
    {
        var clients = hub.Clients.Group(sessionId.ToString());
        await clients.SendAsync("OperatorMessage", new
        {
            SessionId   = sessionId,
            Message     = message,
            ActorName   = actorName,
            DeliveredAt = deliveredAt,
        }, ct);
        await clients.SendAsync("SessionStateChanged", ct);
    }

    public async Task NotifyStageCompletedAsync(
        Guid sessionId, Guid teamId, int stageOrder, string stageType,
        bool wasCorrect, int pointsEarned, int newScore, int nextStageOrder, bool isLastStage,
        CancellationToken ct = default)
    {
        var clients = hub.Clients.Group(sessionId.ToString());
        await clients.SendAsync("SessionStateChanged", ct);
        await clients.SendAsync("StageCompleted", new
        {
            SessionId      = sessionId,
            TeamId         = teamId,
            StageOrder     = stageOrder,
            StageType      = stageType,
            WasCorrect     = wasCorrect,
            PointsEarned   = pointsEarned,
            NewScore       = newScore,
            NextStageOrder = nextStageOrder,
            IsLastStage    = isLastStage,
        }, ct);
    }

    public Task NotifyClueReleasedAsync(
        Guid sessionId, Guid teamId, string? content,
        double? latitude, double? longitude, int? radiusMeters,
        int clueNumber, bool isAutomatic,
        CancellationToken ct = default)
        => hub.Clients.Group(sessionId.ToString())
            .SendAsync("ClueReleased", new
            {
                SessionId        = sessionId,
                TeamId           = teamId,
                ClueContent      = content,
                ClueLatitude     = latitude,
                ClueLongitude    = longitude,
                ClueRadiusMeters = radiusMeters,
                ClueNumber       = clueNumber,
                IsAutomatic      = isAutomatic,
            }, ct);

    public async Task NotifyTeamPenalizedAsync(
        Guid sessionId, Guid teamId, string teamName,
        int points, string reason, int newScore, string actorName,
        CancellationToken ct = default)
    {
        var clients = hub.Clients.Group(sessionId.ToString());
        await clients.SendAsync("SessionStateChanged", ct);
        await clients.SendAsync("TeamPenalized", new
        {
            SessionId = sessionId,
            TeamId    = teamId,
            TeamName  = teamName,
            Points    = points,
            Reason    = reason,
            NewScore  = newScore,
            ActorName = actorName,
        }, ct);
    }
}
