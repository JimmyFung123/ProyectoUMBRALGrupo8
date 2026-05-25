namespace SessionService.Application.Sessions.Queries.GetSessionRanking;

/// <summary>
/// Public ranking projection returned by SessionService (HU-21).
/// Mirrors the optimized read model from TeamService — kept as a record on the
/// orchestrator so participants/operators never call TeamService directly.
/// </summary>
public record SessionRankingResponse(
    Guid SessionId,
    string SessionStatus,
    DateTime GeneratedAt,
    IReadOnlyList<SessionRankingTeamDto> Teams);

public record SessionRankingTeamDto(
    Guid TeamId,
    string Name,
    int Score,
    int Rank,
    int CurrentStageOrder,
    bool IsConnected,
    DateTime? LastStageCompletedAt);
