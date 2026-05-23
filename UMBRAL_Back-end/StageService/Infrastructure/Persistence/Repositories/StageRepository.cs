namespace StageService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using StageService.Domain.Stages;

public class StageRepository : IStageRepository
{
    private readonly StagesDbContext _context;
    public StageRepository(StagesDbContext context) => _context = context;

    public async Task<List<Stage>> GetByMissionIdAsync(Guid missionId, CancellationToken cancellationToken = default)
        => await _context.Stages
            .Include(s => s.Options)
            .Where(s => s.MissionId == missionId)
            .OrderBy(s => s.Order)
            .ToListAsync(cancellationToken);

    public async Task<Stage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Stages
            .Include(s => s.Options)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task AddAsync(Stage stage, CancellationToken cancellationToken = default)
        => await _context.Stages.AddAsync(stage, cancellationToken);

    public Task DeleteAsync(Stage stage, CancellationToken cancellationToken = default)
    {
        _context.Stages.Remove(stage);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
