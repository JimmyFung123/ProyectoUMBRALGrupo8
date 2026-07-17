namespace SessionService.Application.Statistics.Queries.GetDashboardStatistics;

using MediatR;

/// <summary>
/// Handler for the admin statistics dashboard (HU-25).
///
/// Delegates both aggregations to <see cref="IStatisticsReadRepository"/>,
/// which issues one SQL <c>GROUP BY</c> per metric over the fact table —
/// the dashboard <b>never</b> touches the live <c>Sessions</c> or
/// <c>Teams</c> tables, so opening the dashboard cannot slow down ongoing
/// gameplay writes (criterion 2 of HU-25).
///
/// The only arithmetic done in C# is the percent calculation on the
/// effectiveness DTO — a single integer division per row, well-defined for
/// zero answers (returns 0%).
/// </summary>
public class GetDashboardStatisticsQueryHandler
    : IRequestHandler<GetDashboardStatisticsQuery, DashboardStatisticsDto>
{
    private readonly IStatisticsReadRepository _statisticsRepository;

    public GetDashboardStatisticsQueryHandler(IStatisticsReadRepository statisticsRepository)
        => _statisticsRepository = statisticsRepository;

    public async Task<DashboardStatisticsDto> Handle(
        GetDashboardStatisticsQuery request,
        CancellationToken cancellationToken)
    {
        var timeRows = await _statisticsRepository.GetAverageTimePerStageAsync(
            request.MissionId, cancellationToken);

        var effectivenessRows = await _statisticsRepository.GetAnswerEffectivenessPerStageAsync(
            request.MissionId, cancellationToken);

        var timeDtos = timeRows
            .Select(r => new StageTimeStatDto(r.StageOrder, r.AverageSeconds, r.SampleSize))
            .ToList();

        var effectivenessDtos = effectivenessRows
            .Select(r => new StageEffectivenessStatDto(
                StageOrder: r.StageOrder,
                CorrectCount: r.CorrectCount,
                TotalAnswers: r.TotalAnswers,
                CorrectPercentage: r.TotalAnswers == 0
                    ? 0d
                    : Math.Round(100d * r.CorrectCount / r.TotalAnswers, 2)))
            .ToList();

        return new DashboardStatisticsDto(
            MissionId: request.MissionId,
            GeneratedAt: DateTime.UtcNow,
            AverageTimePerStage: timeDtos,
            EffectivenessPerStage: effectivenessDtos);
    }
}
