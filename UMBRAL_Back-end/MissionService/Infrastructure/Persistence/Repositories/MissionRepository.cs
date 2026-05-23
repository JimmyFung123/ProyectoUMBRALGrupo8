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
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Mission>> GetAllAsync(MissionStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Missions.AsQueryable();

        if (status.HasValue)
            query = query.Where(m => m.Status == status.Value);

        return await query.OrderBy(m => m.Name).ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsWithNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => await _context.Missions
            .AnyAsync(m => m.Name == name && (excludeId == null || m.Id != excludeId), cancellationToken);

    public Task<bool> HasActiveSessionsAsync(Guid missionId, CancellationToken cancellationToken = default)
    {
        // Cross-service check: SessionService owns session data.
        // In production, call SessionService via HTTP: GET /api/sessions?missionId={id}&status=InProgress
        // For now returns false (no blocking sessions during design phase).
        return Task.FromResult(false);
    }

    public async Task AddAsync(Mission mission, CancellationToken cancellationToken = default)
        => await _context.Missions.AddAsync(mission, cancellationToken);

    public Task UpdateAsync(Mission mission, CancellationToken cancellationToken = default)
    {
        // The entity was loaded from this same DbContext instance via GetByIdAsync,
        // so EF Core is already tracking all scalar changes automatically.
        // Do NOT call _context.Missions.Update(mission) here — it would mark
        // newly-added child entities as Modified instead of Added.
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
