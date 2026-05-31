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

    public async Task<bool> ExistsWithQrCodeAsync(string qrCode, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => await _context.Stages
            .AnyAsync(s => s.QrCode == qrCode && (excludeId == null || s.Id != excludeId), cancellationToken);

    public async Task RemoveOptionsAsync(Guid stageId, CancellationToken cancellationToken = default)
    {
        // Bulk-delete directly in the DB so the EF change tracker never has
        // to reconcile the deletion with the Stage navigation collection.
        // This avoids the FK/orphan-removal 500 that occurs when EF tries to
        // process Options.Clear() on a collection whose items were already
        // detached or marked Deleted in the same unit of work.
        await _context.TriviaOptions
            .Where(o => o.StageId == stageId)
            .ExecuteDeleteAsync(cancellationToken);

        // Detach any locally-tracked instances so SaveChangesAsync does not
        // attempt a redundant DELETE (rows are already gone from the DB).
        foreach (var entry in _context.ChangeTracker.Entries<TriviaOption>()
                     .Where(e => e.Entity.StageId == stageId)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    public async Task AddOptionsAsync(IEnumerable<TriviaOption> options, CancellationToken cancellationToken = default)
    {
        // Insert the new options directly — bypasses the Stage.Options
        // navigation collection so there is no change-tracker conflict with
        // the ExecuteDeleteAsync that ran just before this call.
        await _context.TriviaOptions.AddRangeAsync(options, cancellationToken);
    }
}
