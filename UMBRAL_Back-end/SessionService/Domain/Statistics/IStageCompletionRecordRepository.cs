namespace SessionService.Domain.Statistics;

/// <summary>
/// Write-side port for the analytics fact table (HU-25).
///
/// Two operations: append a record from a gameplay handler, and bulk-flip
/// the visibility flag when a session is finalized. The dashboard read side
/// has its own port (<c>IStatisticsReadRepository</c>) — they are kept
/// separate so the read path can stay <c>AsNoTracking</c> and lean on
/// SQL-side aggregations without entanglements with the write surface.
/// </summary>
public interface IStageCompletionRecordRepository
{
    Task AddAsync(StageCompletionRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks every record of the session as visible to the dashboard. Called
    /// once when the session transitions to <c>Finalized</c>.
    /// Returns the number of rows that were promoted.
    /// </summary>
    Task<int> MarkSessionIncludedAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
