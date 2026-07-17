namespace UMBRAL_Back_end.Domain.Missions;

public interface IStageCountLookupRepository
{
    Task<StageCountLookup?> GetByMissionIdAsync(Guid missionId, CancellationToken cancellationToken = default);
    Task AddAsync(StageCountLookup lookup, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
