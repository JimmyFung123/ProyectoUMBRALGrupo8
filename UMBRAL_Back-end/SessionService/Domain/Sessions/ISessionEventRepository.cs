namespace SessionService.Domain.Sessions;

public interface ISessionEventRepository
{
    /// <summary>Returns the most recent events for a session, newest first.</summary>
    Task<IReadOnlyList<SessionEvent>> GetRecentBySessionIdAsync(
        Guid sessionId,
        int maxCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the complete audit trail of a session ordered chronologically
    /// (oldest first), suitable for a timeline view. Used by HU-22.
    /// </summary>
    Task<IReadOnlyList<SessionEvent>> GetAllBySessionIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task AddAsync(SessionEvent sessionEvent, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
