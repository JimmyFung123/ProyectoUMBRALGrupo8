namespace StageService.Domain.Stages;
public interface IStageRepository
{
    Task<List<Stage>> GetByMissionIdAsync(Guid missionId, CancellationToken cancellationToken = default);
    Task<Stage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Stage stage, CancellationToken cancellationToken = default);
    Task DeleteAsync(Stage stage, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if any stage already has the given QR code.
    /// Pass <paramref name="excludeId"/> when updating a stage to exclude it from the check.
    /// </summary>
    Task<bool> ExistsWithQrCodeAsync(string qrCode, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all existing trivia options for a stage directly in the DB
    /// (bypasses the EF change tracker to avoid FK/orphan conflicts).
    /// </summary>
    Task RemoveOptionsAsync(Guid stageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a set of new trivia options directly to the DB without going
    /// through the Stage navigation collection (avoids change-tracker conflicts
    /// when combined with <see cref="RemoveOptionsAsync"/>).
    /// </summary>
    Task AddOptionsAsync(IEnumerable<TriviaOption> options, CancellationToken cancellationToken = default);
}
