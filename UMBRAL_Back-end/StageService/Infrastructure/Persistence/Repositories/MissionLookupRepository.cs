namespace StageService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using StageService.Domain.MissionLookup;

public class MissionLookupRepository : IMissionLookupRepository
{
    private readonly StagesDbContext _context;
    public MissionLookupRepository(StagesDbContext context) => _context = context;

    public async Task<MissionLookup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.MissionsLookup.FindAsync([id], cancellationToken);

    public async Task AddAsync(MissionLookup lookup, CancellationToken cancellationToken = default)
        => await _context.MissionsLookup.AddAsync(lookup, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
