namespace SessionService.Domain.Sessions;

public interface ISessionRepository
{
    Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Session>> GetAllAsync(Guid? missionId = null, SessionStatus? status = null, CancellationToken ct = default);
    Task AddAsync(Session session, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
