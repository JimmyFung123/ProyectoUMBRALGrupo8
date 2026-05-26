namespace TeamService.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using TeamService.Domain.Rankings;

/// <summary>
/// EF adapter for the CQRS ranking read model (HU-24).
///
/// Read path: <c>SELECT … WHERE SessionId = ? ORDER BY Position</c> — the
/// composite index on <c>(SessionId, Position)</c> lets PostgreSQL stream the
/// rows in physical order, so the query handler does zero work after the
/// round-trip.
/// </summary>
public class RankingProjectionRepository : IRankingProjectionRepository
{
    private readonly TeamsDbContext _context;

    public RankingProjectionRepository(TeamsDbContext context) => _context = context;

    public async Task<IReadOnlyList<RankingProjection>> GetBySessionIdOrderedAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.RankingProjections
            .AsNoTracking() // pure read path — skip the change tracker for the query handler
            .Where(p => p.SessionId == sessionId)
            .OrderBy(p => p.Position)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(RankingProjection projection, CancellationToken cancellationToken = default)
        => await _context.RankingProjections.AddAsync(projection, cancellationToken);

    public async Task DeleteBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var rows = await _context.RankingProjections
            .Where(p => p.SessionId == sessionId)
            .ToListAsync(cancellationToken);

        _context.RankingProjections.RemoveRange(rows);
    }
}
