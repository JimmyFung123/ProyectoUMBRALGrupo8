namespace SessionService.Domain.Sessions;

public interface ISessionRepository
{
    Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Session>> GetAllAsync(Guid? missionId = null, SessionStatus? status = null, CancellationToken ct = default);
    Task<IReadOnlyList<Session>> GetAllInProgressAsync(CancellationToken ct = default);

    /// <summary>
    /// RB-15 — Returns true if the mission has any session in a non-terminal
    /// state (Pending, InProgress, or Paused). Used by MissionService to block
    /// deactivation while sessions are still live or scheduled.
    /// </summary>
    Task<bool> HasNonTerminalSessionsAsync(Guid missionId, CancellationToken ct = default);
    Task AddAsync(Session session, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<Session?> GetByAccessCodeAsync(string code, CancellationToken ct = default);
}
