namespace TeamService.Application.Teams.Queries.GetSessionRanking;

/// <summary>
/// Optimized read model for the live session ranking (HU-21).
/// Carries only the fields the leaderboard needs — no clue counters, no flags —
/// so it can be cached or served without touching the transactional write path.
/// Sort order is decided server-side (Score desc, then LastStageCompletedAt asc).
/// </summary>
public record SessionRankingTeamDto(
    Guid TeamId,
    string Name,
    int Score,
    int Rank,
    int CurrentStageOrder,
    bool IsConnected,
    DateTime? LastStageCompletedAt);

/// <summary>
/// Top-level ranking response: ordered list of teams plus the server timestamp
/// the snapshot was generated, so the client can show "actualizado hace X seg".
/// </summary>
public record SessionRankingDto(
    Guid SessionId,
    DateTime GeneratedAt,
    IReadOnlyList<SessionRankingTeamDto> Teams);
