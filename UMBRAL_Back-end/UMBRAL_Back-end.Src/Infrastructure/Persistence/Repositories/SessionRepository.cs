namespace UMBRAL_Back_end.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using UMBRAL_Back_end.Domain.Sessions;

public class SessionRepository : ISessionRepository
{
    private readonly AppDbContext _context;

    public SessionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Sessions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Session>> GetAllAsync(
        Guid? missionId = null,
        SessionStatus? status = null,
        CancellationToken ct = default)
    {
        var query = _context.Sessions.AsQueryable();

        if (missionId.HasValue)
            query = query.Where(s => s.MissionId == missionId.Value);

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        return await query.OrderByDescending(s => s.CreatedAt).ToListAsync(ct);
    }

    public async Task AddAsync(Session session, CancellationToken ct = default)
        => await _context.Sessions.AddAsync(session, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
