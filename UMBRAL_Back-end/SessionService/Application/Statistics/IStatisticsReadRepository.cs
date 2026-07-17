namespace SessionService.Application.Statistics;

/// <summary>
/// Read-side port for the analytics dashboard (HU-25).
///
/// Both methods translate to a single SQL aggregation each (<c>AVG</c> /
/// <c>SUM CASE WHEN</c>) over the fact table, filtered to records whose
/// parent session was finalized. The implementation never JOINs against
/// <c>Sessions</c> or <c>Teams</c> — gameplay writes therefore can't be
/// blocked or slowed down by an admin opening the dashboard (criterion 2).
/// </summary>
public interface IStatisticsReadRepository
{
    Task<IReadOnlyList<StageAverageTime>> GetAverageTimePerStageAsync(
        Guid? missionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StageAnswerEffectiveness>> GetAnswerEffectivenessPerStageAsync(
        Guid? missionId,
        CancellationToken cancellationToken = default);
}

/// <summary>One row per stage order — average seconds taken by all teams.</summary>
public record StageAverageTime(int StageOrder, double AverageSeconds, int SampleSize);

/// <summary>One row per trivia stage — correct vs total answers (TreasureHunt is excluded).</summary>
public record StageAnswerEffectiveness(int StageOrder, int CorrectCount, int TotalAnswers);
