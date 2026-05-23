namespace SessionService.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SessionService.Domain.Sessions;

public class TeamRepository : ITeamRepository
{
    private readonly SessionsDbContext _context;

    public TeamRepository(SessionsDbContext context) => _context = context;

    public async Task<IReadOnlyList<Team>> GetBySessionIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
        => await _context.Teams
            .Where(t => t.SessionId == sessionId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Team team, CancellationToken cancellationToken = default)
        => await _context.Teams.AddAsync(team, cancellationToken);

    public async Task DeleteBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var teams = await _context.Teams
            .Where(t => t.SessionId == sessionId)
            .ToListAsync(cancellationToken);
        _context.Teams.RemoveRange(teams);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
