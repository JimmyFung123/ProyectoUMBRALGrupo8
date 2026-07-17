namespace SessionService.Application.Missions.Composite;

/// <summary>
/// Aggregated, read-only summary produced by walking a mission tree once.
/// </summary>
public sealed record MissionStructureSummary(
    int StageCount,
    int ClueCount,
    int TriviaStages,
    int TreasureHuntStages,
    int TotalScore,
    int StagesWithoutClues);

/// <summary>
/// VISITOR PATTERN — concrete visitor. Walks a mission tree accumulating
/// counts and totals, then exposes them via <see cref="Build"/>. Adding this
/// operation required no changes to the component classes.
/// </summary>
public sealed class MissionSummaryVisitor : IMissionComponentVisitor
{
    private int _stageCount;
    private int _clueCount;
    private int _triviaStages;
    private int _treasureHuntStages;
    private int _totalScore;
    private int _stagesWithoutClues;

    public void Visit(MissionComponent mission)
    {
        // The mission's own total score already aggregates the sub-tree.
        _totalScore = mission.TotalScore();
    }

    public void Visit(StageComponent stage)
    {
        _stageCount++;

        if (string.Equals(stage.Type, "TreasureHunt", StringComparison.OrdinalIgnoreCase))
            _treasureHuntStages++;
        else
            _triviaStages++;

        if (stage.Children.Count == 0)
            _stagesWithoutClues++;
    }

    public void Visit(ClueComponent clue) => _clueCount++;

    public MissionStructureSummary Build() => new(
        StageCount: _stageCount,
        ClueCount: _clueCount,
        TriviaStages: _triviaStages,
        TreasureHuntStages: _treasureHuntStages,
        TotalScore: _totalScore,
        StagesWithoutClues: _stagesWithoutClues);
}
