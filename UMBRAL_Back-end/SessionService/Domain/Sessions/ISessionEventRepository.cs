namespace SessionService.Domain.Sessions;

public interface ISessionEventRepository
{
    /// <summary>Returns the most recent events for a session, newest first.</summary>
    Task<IReadOnlyList<SessionEvent>> GetRecentBySessionIdAsync(
        Guid sessionId,
        int maxCount,
        CancellationToken cancellationToken = default);

    Task AddAsync(SessionEvent sessionEvent, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
