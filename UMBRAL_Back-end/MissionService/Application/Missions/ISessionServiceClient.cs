namespace UMBRAL_Back_end.Application.Missions;

/// <summary>
/// Port to SessionService for cross-service business rule validation.
/// </summary>
public interface ISessionServiceClient
{
    /// <summary>
    /// RB-15 — Returns true if the given mission has at least one session
    /// currently in progress. Returns false on error or if unreachable.
    /// </summary>
    Task<bool> HasActiveSessionsAsync(Guid missionId, CancellationToken cancellationToken = default);
}
