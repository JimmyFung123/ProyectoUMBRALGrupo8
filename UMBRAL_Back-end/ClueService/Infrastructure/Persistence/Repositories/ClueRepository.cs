namespace ClueService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using ClueService.Domain.Clues;

public class ClueRepository : IClueRepository
{
    private readonly CluesDbContext _context;
    public ClueRepository(CluesDbContext context) => _context = context;

    public async Task<List<Clue>> GetByStageIdAsync(Guid stageId, CancellationToken cancellationToken = default)
        => await _context.Clues.Where(c => c.StageId == stageId).OrderBy(c => c.Order).ToListAsync(cancellationToken);

    public async Task<Clue?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Clues.FindAsync([id], cancellationToken);

    public async Task AddAsync(Clue clue, CancellationToken cancellationToken = default)
        => await _context.Clues.AddAsync(clue, cancellationToken);

    public Task DeleteAsync(Clue clue, CancellationToken cancellationToken = default)
    {
        _context.Clues.Remove(clue);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
