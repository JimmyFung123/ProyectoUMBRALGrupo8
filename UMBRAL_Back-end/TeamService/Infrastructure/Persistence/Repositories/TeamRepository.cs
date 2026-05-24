namespace TeamService.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using TeamService.Domain.Teams;

public class TeamRepository : ITeamRepository
{
    private readonly TeamsDbContext _context;

    public TeamRepository(TeamsDbContext context) => _context = context;

    public async Task<Team?> GetByIdAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Teams
            .FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);
    }

    public async Task<IReadOnlyList<Team>> GetBySessionIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Teams
            .Where(t => t.SessionId == sessionId)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteBySessionIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var teams = await _context.Teams
            .Where(t => t.SessionId == sessionId)
            .ToListAsync(cancellationToken);

        _context.Teams.RemoveRange(teams);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
