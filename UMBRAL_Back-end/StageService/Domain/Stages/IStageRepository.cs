namespace StageService.Domain.Stages;
public interface IStageRepository
{
    Task<List<Stage>> GetByMissionIdAsync(Guid missionId, CancellationToken cancellationToken = default);
    Task<Stage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Stage stage, CancellationToken cancellationToken = default);
    Task DeleteAsync(Stage stage, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
