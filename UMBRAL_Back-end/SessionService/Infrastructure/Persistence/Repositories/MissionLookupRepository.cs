namespace SessionService.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SessionService.Domain.MissionLookup;

public class MissionLookupRepository : IMissionLookupRepository
{
    private readonly SessionsDbContext _context;

    public MissionLookupRepository(SessionsDbContext context) => _context = context;

    public Task<MissionLookup?> GetByIdAsync(Guid missionId, CancellationToken ct = default)
        => _context.MissionsLookup.FirstOrDefaultAsync(m => m.Id == missionId, ct);

    public async Task AddAsync(MissionLookup lookup, CancellationToken ct = default)
        => await _context.MissionsLookup.AddAsync(lookup, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
