namespace ClueService.Domain.Clues;
public interface IClueRepository
{
    Task<List<Clue>> GetByStageIdAsync(Guid stageId, CancellationToken cancellationToken = default);
    Task<Clue?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Clue clue, CancellationToken cancellationToken = default);
    Task DeleteAsync(Clue clue, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
