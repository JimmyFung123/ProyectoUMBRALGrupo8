namespace StageService.Domain.MissionLookup;
public interface IMissionLookupRepository
{
    Task<MissionLookup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(MissionLookup lookup, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
