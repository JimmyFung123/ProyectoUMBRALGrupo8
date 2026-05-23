namespace UMBRAL_Back_end.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using UMBRAL_Back_end.Domain.Missions;

public class MissionRepository : IMissionRepository
{
    private readonly AppDbContext _context;

    public MissionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Mission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Missions
            .Include(m => m.Stages)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Mission>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Missions
            .Include(m => m.Stages)
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);

    public async Task<bool> ExistsWithNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => await _context.Missions
            .AnyAsync(m => m.Name == name && (excludeId == null || m.Id != excludeId), cancellationToken);

    public Task<bool> HasActiveSessionsAsync(Guid missionId, CancellationToken cancellationToken = default)
    {
        // TODO: query the Session bounded context once HU-3 is implemented.
        // This will likely be an HTTP call to the Session microservice or a read-model query.
        return Task.FromResult(false);
    }

    public async Task AddAsync(Mission mission, CancellationToken cancellationToken = default)
        => await _context.Missions.AddAsync(mission, cancellationToken);

    public Task UpdateAsync(Mission mission, CancellationToken cancellationToken = default)
    {
        _context.Missions.Update(mission);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
