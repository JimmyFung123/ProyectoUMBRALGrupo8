namespace UMBRAL_Back_end.Domain.Missions;

public interface IMissionRepository
{
    Task<Mission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Mission>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsWithNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> HasActiveSessionsAsync(Guid missionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if a stage with the given QR code already exists,
    /// optionally excluding a specific stage (for update scenarios).
    /// </summary>
    Task<bool> HasDuplicateQrCodeAsync(string qrCode, Guid? excludeStageId = null, CancellationToken cancellationToken = default);

    Task AddAsync(Mission mission, CancellationToken cancellationToken = default);
    Task UpdateAsync(Mission mission, CancellationToken cancellationToken = default);
    Task AddStageAsync(MissionStage stage, CancellationToken cancellationToken = default);
    Task RemoveStageAsync(MissionStage stage, CancellationToken cancellationToken = default);
    Task ReplaceStageOptionsAsync(Guid stageId, IEnumerable<(string Text, bool IsCorrect)> options, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
