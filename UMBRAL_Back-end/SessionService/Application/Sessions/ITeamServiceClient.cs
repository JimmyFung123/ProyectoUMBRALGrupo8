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

    /// <summary>Forces a team to advance to the given next stage, earning 0 points.</summary>
    Task<bool> ForceAdvanceTeamAsync(Guid teamId, int nextStageOrder, CancellationToken cancellationToken);
}

public record TeamProgressItem(
    Guid Id,
    string Name,
    int CurrentStageOrder,
    int CluesReceivedCurrentStage,
    DateTime? ClueTimerResetAt,
    bool LastClueWasAutomatic);
