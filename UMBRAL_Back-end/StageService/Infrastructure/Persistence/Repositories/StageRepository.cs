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
        // Bulk delete vía EF Core 7+. Borra las TriviaOptions directamente en
        // SQL sin pasar por el change tracker, evitando el problema del
        // orphan-removal que producía un 500 al editar una etapa con
        // opciones existentes (Stage.Options.Clear() no marcaba los items
        // tracked para DELETE cuando ya habían quedado en estado Detached
        // dentro del mismo SaveChanges).
        await _context.TriviaOptions
            .Where(o => o.StageId == stageId)
            .ExecuteDeleteAsync(cancellationToken);

        // Quita las referencias locales que pudieran estar tracked, para que
        // el siguiente SaveChanges no intente re-insertarlas.
        foreach (var entry in _context.ChangeTracker.Entries<TriviaOption>().ToList())
        {
            if (entry.Entity.StageId == stageId)
                entry.State = EntityState.Detached;
        }
    }
}
