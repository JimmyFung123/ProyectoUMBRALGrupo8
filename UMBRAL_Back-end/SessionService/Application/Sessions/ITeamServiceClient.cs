namespace SessionService.Application.Sessions;

/// <summary>
/// Port to the TeamService used for cross-service business rule validation.
/// </summary>
public interface ITeamServiceClient
{
    /// <summary>
    /// Returns true if at least one team is enrolled in the given session.
    /// </summary>
    Task<bool> HasEnrolledTeamsAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Records the release of the next clue to a team. Returns the new clues-received count,
    /// or -1 when all clues were already released (exhausted).
    /// </summary>
    Task<int> ReleaseClueAsync(Guid teamId, int totalCluesForStage, CancellationToken cancellationToken, bool isAutomatic = false);

    /// <summary>Gets current progress for all teams in a session (used by auto-release worker).</summary>
    Task<IReadOnlyList<TeamProgressItem>> GetTeamProgressAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Applies a penalty to a team, subtracting the given points. Returns the new score, or int.MinValue on error.</summary>
    Task<int> PenalizeTeamAsync(Guid teamId, int points, string reason, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a participant from the team. If the team is left with zero members,
    /// TeamService deletes it entirely (frees the invite code). Best-effort: a failure
    /// here must not block the participant from leaving on the front-end.
    /// </summary>
    Task LeaveTeamAsync(Guid teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a team to advance to the given next stage, earning 0 points.
    /// Returns <c>null</c> on error. Otherwise carries the seconds the team
    /// spent on the skipped stage — used by HU-25 to record the analytics row.
    /// </summary>
    Task<StageTransitionResult?> ForceAdvanceTeamAsync(Guid teamId, int nextStageOrder, CancellationToken cancellationToken);

    /// <summary>
    /// Records the outcome of a piece of stage evidence (trivia answer or QR scan):
    /// adjusts the team's score and advances to nextStageOrder.
    /// Returns <c>null</c> on error. Otherwise carries the new score AND the
    /// seconds elapsed since the team entered the stage just resolved — the
    /// timing data feeds the HU-25 analytics dashboard.
    /// </summary>
    Task<StageTransitionResult?> RecordEvidenceOutcomeAsync(Guid teamId, bool isCorrect, int scoreChange, int nextStageOrder, CancellationToken cancellationToken);

    /// <summary>
    /// RB-18: Returns true if every team enrolled in the session has at least
    /// <paramref name="minMembers"/> members. Returns false if any team falls below
    /// the minimum or if the TeamService is unreachable.
    /// </summary>
    Task<bool> AllTeamsMeetMinimumMembersAsync(Guid sessionId, int minMembers, CancellationToken cancellationToken);

    /// <summary>Returns basic info for a single team by ID.</summary>
    Task<TeamInfoItem?> GetTeamByIdAsync(Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the live ranking snapshot (HU-21). Reads the optimized projection
    /// exposed by TeamService — never goes through the write/command path.
    /// On error, returns null so the proxy can surface a 503-ish response.
    /// </summary>
    Task<SessionRankingSnapshot?> GetSessionRankingAsync(Guid sessionId, CancellationToken cancellationToken);
}

public record SessionRankingSnapshot(
    Guid SessionId,
    DateTime GeneratedAt,
    IReadOnlyList<SessionRankingTeamItem> Teams);

public record SessionRankingTeamItem(
    Guid TeamId,
    string Name,
    int Score,
    int Rank,
    int CurrentStageOrder,
    bool IsConnected,
    DateTime? LastStageCompletedAt);

public record TeamProgressItem(
    Guid Id,
    string Name,
    int CurrentStageOrder,
    int CluesReceivedCurrentStage,
    DateTime? ClueTimerResetAt,
    bool LastClueWasAutomatic);

public record TeamInfoItem(
    Guid Id,
    string Name,
    int CurrentStageOrder);

/// <summary>
/// Outcome of a TeamService stage-advance call (HU-25). The score field
/// carries the post-mutation score; <c>ElapsedSeconds</c> is the time the
/// team spent on the stage being abandoned — required by SessionService to
/// write the analytics fact row.
/// </summary>
public record StageTransitionResult(int NewScore, int ElapsedSeconds);
