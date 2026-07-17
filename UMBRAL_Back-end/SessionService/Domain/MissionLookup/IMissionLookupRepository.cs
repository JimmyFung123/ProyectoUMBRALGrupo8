namespace SessionService.Domain.MissionLookup;

public interface IMissionLookupRepository
{
    Task<MissionLookup?> GetByIdAsync(Guid missionId, CancellationToken ct = default);
    Task AddAsync(MissionLookup lookup, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
