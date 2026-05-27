namespace SessionService.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SessionService.Domain.Statistics;

/// <summary>
/// EF adapter for the analytics fact table (HU-25). The bulk update uses
/// EF Core's <c>ExecuteUpdateAsync</c> so the "promote on finalize" path
/// runs as a single SQL statement instead of loading rows into memory.
/// </summary>
public class StageCompletionRecordRepository : IStageCompletionRecordRepository
{
    private readonly SessionsDbContext _context;

    public StageCompletionRecordRepository(SessionsDbContext context) => _context = context;

    public async Task AddAsync(StageCompletionRecord record, CancellationToken cancellationToken = default)
        => await _context.StageCompletionRecords.AddAsync(record, cancellationToken);

    public async Task<int> MarkSessionIncludedAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await _context.StageCompletionRecords
            .Where(r => r.SessionId == sessionId && !r.IncludedInStatistics)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.IncludedInStatistics, true),
                cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
