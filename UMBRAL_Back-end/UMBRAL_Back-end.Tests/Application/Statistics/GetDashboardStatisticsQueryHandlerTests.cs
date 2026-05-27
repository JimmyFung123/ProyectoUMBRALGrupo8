namespace UMBRAL_Back_end.Tests.Application.Statistics;

using FluentAssertions;
using Moq;
using SessionService.Application.Statistics;
using SessionService.Application.Statistics.Queries.GetDashboardStatistics;
using Xunit;

/// <summary>
/// HU-25: the dashboard handler is a thin adapter over the read repository.
/// It must (1) pass the optional mission filter through, (2) map the
/// aggregated rows to DTOs 1-to-1, and (3) compute the answer-effectiveness
/// percent without losing precision or dividing by zero.
/// </summary>
public class GetDashboardStatisticsQueryHandlerTests
{
    private readonly Mock<IStatisticsReadRepository> _repoMock = new();
    private readonly GetDashboardStatisticsQueryHandler _handler;

    public GetDashboardStatisticsQueryHandlerTests()
    {
        _handler = new GetDashboardStatisticsQueryHandler(_repoMock.Object);
    }

    // ── No data ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNoFinalizedSessions_ReturnsEmptySections()
    {
        _repoMock
            .Setup(r => r.GetAverageTimePerStageAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StageAverageTime>());
        _repoMock
            .Setup(r => r.GetAnswerEffectivenessPerStageAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StageAnswerEffectiveness>());

        var result = await _handler.Handle(new GetDashboardStatisticsQuery(MissionId: null), default);

        result.MissionId.Should().BeNull();
        result.AverageTimePerStage.Should().BeEmpty();
        result.EffectivenessPerStage.Should().BeEmpty();
    }

    // ── Mission filter pass-through ───────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenMissionIdProvided_PassesItThroughToBothRepositoryCalls()
    {
        var missionId = Guid.NewGuid();
        _repoMock
            .Setup(r => r.GetAverageTimePerStageAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StageAverageTime>());
        _repoMock
            .Setup(r => r.GetAnswerEffectivenessPerStageAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StageAnswerEffectiveness>());

        var result = await _handler.Handle(new GetDashboardStatisticsQuery(missionId), default);

        result.MissionId.Should().Be(missionId);
        _repoMock.Verify(
            r => r.GetAverageTimePerStageAsync(missionId, It.IsAny<CancellationToken>()),
            Times.Once);
        _repoMock.Verify(
            r => r.GetAnswerEffectivenessPerStageAsync(missionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Stage time mapping ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MapsStageTimeRowsOneToOne()
    {
        _repoMock
            .Setup(r => r.GetAverageTimePerStageAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StageAverageTime>
            {
                new(StageOrder: 1, AverageSeconds: 45.5, SampleSize: 12),
                new(StageOrder: 2, AverageSeconds: 90.0, SampleSize: 10),
                new(StageOrder: 3, AverageSeconds: 120.25, SampleSize: 8),
            });
        _repoMock
            .Setup(r => r.GetAnswerEffectivenessPerStageAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StageAnswerEffectiveness>());

        var result = await _handler.Handle(new GetDashboardStatisticsQuery(null), default);

        result.AverageTimePerStage.Should().HaveCount(3);
        result.AverageTimePerStage[0].Should()
            .BeEquivalentTo(new StageTimeStatDto(1, 45.5, 12));
        result.AverageTimePerStage[1].Should()
            .BeEquivalentTo(new StageTimeStatDto(2, 90.0, 10));
        result.AverageTimePerStage[2].Should()
            .BeEquivalentTo(new StageTimeStatDto(3, 120.25, 8));
    }

    // ── Effectiveness percent calculation ─────────────────────────────────────

    [Fact]
    public async Task Handle_ComputesCorrectPercentage_ForEachStage()
    {
        _repoMock
            .Setup(r => r.GetAverageTimePerStageAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StageAverageTime>());
        _repoMock
            .Setup(r => r.GetAnswerEffectivenessPerStageAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StageAnswerEffectiveness>
            {
                new(StageOrder: 1, CorrectCount: 8, TotalAnswers: 10),  // 80%
                new(StageOrder: 2, CorrectCount: 3, TotalAnswers: 4),   // 75%
                new(StageOrder: 3, CorrectCount: 1, TotalAnswers: 3),   // 33.33%
            });

        var result = await _handler.Handle(new GetDashboardStatisticsQuery(null), default);

        result.EffectivenessPerStage[0].CorrectPercentage.Should().Be(80.0);
        result.EffectivenessPerStage[1].CorrectPercentage.Should().Be(75.0);
        result.EffectivenessPerStage[2].CorrectPercentage.Should().Be(33.33);
    }

    [Fact]
    public async Task Handle_WhenStageHasZeroAnswers_PercentageIsZero_NotDivisionError()
    {
        _repoMock
            .Setup(r => r.GetAverageTimePerStageAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StageAverageTime>());
        _repoMock
            .Setup(r => r.GetAnswerEffectivenessPerStageAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StageAnswerEffectiveness>
            {
                new(StageOrder: 1, CorrectCount: 0, TotalAnswers: 0),
            });

        var result = await _handler.Handle(new GetDashboardStatisticsQuery(null), default);

        result.EffectivenessPerStage[0].CorrectPercentage.Should().Be(0d);
        // Guards against the temptation of returning NaN — front would have
        // to special-case it which is worse than just showing 0%.
        result.EffectivenessPerStage[0].TotalAnswers.Should().Be(0);
    }

    // ── GeneratedAt timestamp ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_StampsGeneratedAt_WithCurrentUtcTime()
    {
        _repoMock
            .Setup(r => r.GetAverageTimePerStageAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StageAverageTime>());
        _repoMock
            .Setup(r => r.GetAnswerEffectivenessPerStageAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StageAnswerEffectiveness>());

        var before = DateTime.UtcNow;
        var result = await _handler.Handle(new GetDashboardStatisticsQuery(null), default);
        var after = DateTime.UtcNow;

        result.GeneratedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
