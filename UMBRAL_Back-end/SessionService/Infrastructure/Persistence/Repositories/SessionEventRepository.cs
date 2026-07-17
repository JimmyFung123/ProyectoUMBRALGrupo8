namespace SessionService.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SessionService.Domain.Sessions;

public class SessionEventRepository : ISessionEventRepository
{
    private readonly SessionsDbContext _context;

    public SessionEventRepository(SessionsDbContext context) => _context = context;

    public async Task<IReadOnlyList<SessionEvent>> GetRecentBySessionIdAsync(
        Guid sessionId,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        return await _context.SessionEvents
            .Where(e => e.SessionId == sessionId)
            .OrderByDescending(e => e.OccurredAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SessionEvent>> GetAllBySessionIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        // Oldest first — the audit timeline reads top-down chronologically (HU-22).
        return await _context.SessionEvents
            .Where(e => e.SessionId == sessionId)
            .OrderBy(e => e.OccurredAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(SessionEvent sessionEvent, CancellationToken cancellationToken = default)
        => await _context.SessionEvents.AddAsync(sessionEvent, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
