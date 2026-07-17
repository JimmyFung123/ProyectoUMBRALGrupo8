namespace UMBRAL_Back_end.Infrastructure.Persistence.Repositories;

using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Infrastructure.Persistence;

public class StageCountLookupRepository : IStageCountLookupRepository
{
    private readonly AppDbContext _context;
    public StageCountLookupRepository(AppDbContext context) => _context = context;

    public async Task<StageCountLookup?> GetByMissionIdAsync(Guid missionId, CancellationToken cancellationToken = default)
        => await _context.StageCountLookup.FindAsync([missionId], cancellationToken);

    public async Task AddAsync(StageCountLookup lookup, CancellationToken cancellationToken = default)
        => await _context.StageCountLookup.AddAsync(lookup, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
