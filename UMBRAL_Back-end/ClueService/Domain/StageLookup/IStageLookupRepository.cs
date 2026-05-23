namespace ClueService.Domain.StageLookup;
public interface IStageLookupRepository
{
    Task<StageLookup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(StageLookup lookup, CancellationToken cancellationToken = default);
    Task DeleteAsync(StageLookup lookup, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
