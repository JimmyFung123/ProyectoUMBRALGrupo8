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
    Task<int> ReleaseClueAsync(Guid teamId, int totalCluesForStage, CancellationToken cancellationToken);
}
