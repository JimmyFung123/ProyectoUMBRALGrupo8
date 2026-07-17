namespace SessionService.Application.Statistics.Queries.GetDashboardStatistics;

/// <summary>
/// Top-level payload of the admin statistics dashboard (HU-25).
///
/// Carries the two mandatory metrics from the acceptance criteria
/// (promedios de tiempo por etapa + efectividad de respuestas) as already
/// aggregated, plain numbers — the front-end never re-aggregates.
/// </summary>
public record DashboardStatisticsDto(
    Guid? MissionId,
    DateTime GeneratedAt,
    IReadOnlyList<StageTimeStatDto> AverageTimePerStage,
    IReadOnlyList<StageEffectivenessStatDto> EffectivenessPerStage);

/// <summary>
/// One row per stage order — average seconds taken across every team in
/// every finalized session (filter by mission optional). Force-advance
/// rows are excluded server-side because they would skew the average.
/// </summary>
public record StageTimeStatDto(int StageOrder, double AverageSeconds, int SampleSize);

/// <summary>
/// One row per trivia stage — correct vs total answers across every team
/// in every finalized session. <see cref="CorrectPercentage"/> is the
/// derived value the UI shows; it is computed here so the front renders
/// the number without any client-side math.
/// </summary>
public record StageEffectivenessStatDto(
    int StageOrder,
    int CorrectCount,
    int TotalAnswers,
    double CorrectPercentage);
