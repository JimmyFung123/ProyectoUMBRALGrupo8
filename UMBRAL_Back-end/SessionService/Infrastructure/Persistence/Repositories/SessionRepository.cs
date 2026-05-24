namespace SessionService.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SessionService.Domain.Sessions;

public class SessionRepository : ISessionRepository
{
    private readonly SessionsDbContext _context;

    public SessionRepository(SessionsDbContext context) => _context = context;

    public Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.Sessions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Session>> GetAllAsync(
        Guid? missionId = null, SessionStatus? status = null, CancellationToken ct = default)
    {
        var query = _context.Sessions.AsQueryable();
        if (missionId.HasValue) query = query.Where(s => s.MissionId == missionId.Value);
        if (status.HasValue) query = query.Where(s => s.Status == status.Value);
        return await query.OrderByDescending(s => s.CreatedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Session>> GetAllInProgressAsync(CancellationToken ct = default)
    {
        var list = await _context.Sessions
            .Where(s => s.Status == SessionStatus.InProgress)
            .ToListAsync(ct);
        return list;
    }

    public async Task AddAsync(Session session, CancellationToken ct = default)
        => await _context.Sessions.AddAsync(session, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);

    public Task<Session?> GetByAccessCodeAsync(string code, CancellationToken ct = default)
        => _context.Sessions.FirstOrDefaultAsync(
               s => s.AccessCode == code.ToUpperInvariant(), ct);
}
