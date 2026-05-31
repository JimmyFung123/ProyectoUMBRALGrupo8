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
    /// Borra explícitamente todas las opciones de trivia de una etapa.
    /// Evita el escenario en el que EF Core no elimina los huérfanos al
    /// llamar <c>Stage.Options.Clear()</c> y termina lanzando una
    /// excepción de FK no nulo al hacer SaveChanges (causa del 500
    /// reportado al editar una trivia con opciones existentes).
    /// </summary>
    Task RemoveOptionsAsync(Guid stageId, CancellationToken cancellationToken = default);
}
