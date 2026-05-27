namespace SessionService.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SessionService.Application.Statistics;

/// <summary>
/// EF adapter that issues the two aggregation queries powering the
/// statistics dashboard (HU-25).
///
/// Both queries:
///   - Use <c>AsNoTracking</c> — pure read path, no change-tracker overhead.
///   - Filter by <c>IncludedInStatistics=true</c> so only finalized sessions
///     contribute. Active gameplay rows are physically present in the table
///     (we capture as it happens) but invisible until finalize promotes them.
///   - Group server-side in PostgreSQL. The application receives the
///     aggregated numbers directly — there is zero math in C#.
/// </summary>
public class StatisticsReadRepository : IStatisticsReadRepository
{
    private readonly SessionsDbContext _context;

    public StatisticsReadRepository(SessionsDbContext context) => _context = context;

    public async Task<IReadOnlyList<StageAverageTime>> GetAverageTimePerStageAsync(
        Guid? missionId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.StageCompletionRecords
            .AsNoTracking()
            .Where(r => r.IncludedInStatistics
                        && !r.WasForceAdvance); // force-advances skew the average — exclude them

        if (missionId.HasValue)
            query = query.Where(r => r.MissionId == missionId.Value);

        var rows = await query
            .GroupBy(r => r.StageOrder)
            .Select(g => new
            {
                StageOrder = g.Key,
                AverageSeconds = g.Average(r => (double)r.ElapsedSeconds),
                SampleSize = g.Count(),
            })
            .OrderBy(r => r.StageOrder)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new StageAverageTime(r.StageOrder, r.AverageSeconds, r.SampleSize))
            .ToList();
    }

    public async Task<IReadOnlyList<StageAnswerEffectiveness>> GetAnswerEffectivenessPerStageAsync(
        Guid? missionId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.StageCompletionRecords
            .AsNoTracking()
            .Where(r => r.IncludedInStatistics
                        && r.StageType == "Trivia" // effectiveness is meaningful only for trivia
                        && r.WasCorrect.HasValue);

        if (missionId.HasValue)
            query = query.Where(r => r.MissionId == missionId.Value);

        var rows = await query
            .GroupBy(r => r.StageOrder)
            .Select(g => new
            {
                StageOrder = g.Key,
                CorrectCount = g.Count(r => r.WasCorrect == true),
                TotalAnswers = g.Count(),
            })
            .OrderBy(r => r.StageOrder)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new StageAnswerEffectiveness(r.StageOrder, r.CorrectCount, r.TotalAnswers))
            .ToList();
    }
}
