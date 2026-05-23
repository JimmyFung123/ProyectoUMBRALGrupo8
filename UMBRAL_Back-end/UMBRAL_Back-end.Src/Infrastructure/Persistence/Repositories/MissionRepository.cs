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
                .ThenInclude(s => s.Options)
            .Include(m => m.Stages)
                .ThenInclude(s => s.Clues)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Mission>> GetAllAsync(MissionStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Missions
            .Include(m => m.Stages)
                .ThenInclude(s => s.Options)
            .Include(m => m.Stages)
                .ThenInclude(s => s.Clues)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(m => m.Status == status.Value);

        return await query.OrderBy(m => m.Name).ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsWithNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => await _context.Missions
            .AnyAsync(m => m.Name == name && (excludeId == null || m.Id != excludeId), cancellationToken);

    public Task<bool> HasActiveSessionsAsync(Guid missionId, CancellationToken cancellationToken = default)
    {
        // TODO: query Session bounded context once HU-5+ is implemented.
        return Task.FromResult(false);
    }

    public async Task<bool> HasDuplicateQrCodeAsync(string qrCode, Guid? excludeStageId = null, CancellationToken cancellationToken = default)
        => await _context.MissionStages
            .AnyAsync(s => s.QrCode == qrCode && (excludeStageId == null || s.Id != excludeStageId), cancellationToken);

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

    public async Task AddStageAsync(MissionStage stage, CancellationToken cancellationToken = default)
        => await _context.MissionStages.AddAsync(stage, cancellationToken);

    public Task RemoveStageAsync(MissionStage stage, CancellationToken cancellationToken = default)
    {
        _context.MissionStages.Remove(stage);
        return Task.CompletedTask;
    }

    public async Task ReplaceStageOptionsAsync(Guid stageId, IEnumerable<(string Text, bool IsCorrect)> options, CancellationToken cancellationToken = default)
    {
        var existing = await _context.TriviaOptions
            .Where(o => o.StageId == stageId)
            .ToListAsync(cancellationToken);

        _context.TriviaOptions.RemoveRange(existing);

        var newOptions = options
            .Select(o => TriviaOption.Create(stageId, o.Text, o.IsCorrect))
            .ToList();

        await _context.TriviaOptions.AddRangeAsync(newOptions, cancellationToken);
    }

    public async Task AddClueAsync(Clue clue, CancellationToken cancellationToken = default)
        => await _context.Clues.AddAsync(clue, cancellationToken);

    public Task RemoveClueAsync(Clue clue, CancellationToken cancellationToken = default)
    {
        _context.Clues.Remove(clue);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
