namespace ClueService.Infrastructure.Persistence.Repositories;
using ClueService.Domain.StageLookup;

public class StageLookupRepository : IStageLookupRepository
{
    private readonly CluesDbContext _context;
    public StageLookupRepository(CluesDbContext context) => _context = context;

    public async Task<StageLookup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.StagesLookup.FindAsync([id], cancellationToken);

    public async Task AddAsync(StageLookup lookup, CancellationToken cancellationToken = default)
        => await _context.StagesLookup.AddAsync(lookup, cancellationToken);

    public Task DeleteAsync(StageLookup lookup, CancellationToken cancellationToken = default)
    {
        _context.StagesLookup.Remove(lookup);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
