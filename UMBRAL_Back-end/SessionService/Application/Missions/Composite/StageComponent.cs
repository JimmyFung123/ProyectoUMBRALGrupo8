namespace SessionService.Application.Missions.Composite;

/// <summary>
/// COMPOSITE PATTERN — intermediate composite. Wraps a Stage and holds its
/// <see cref="ClueComponent"/> children. Its own score is the stage's base
/// score; clues add 0, so a stage's <see cref="MissionComponentBase.TotalScore"/>
/// equals its base score regardless of how many clues it has.
/// </summary>
public sealed class StageComponent : CompositeMissionComponent
{
    public StageComponent(Guid id, string title, string type, int order, int baseScore)
        : base(id, title)
    {
        Type = type;
        Order = order;
        BaseScore = baseScore;
    }

    public override string ComponentType => "Stage";

    /// <summary>"Trivia" | "TreasureHunt".</summary>
    public string Type { get; }

    public int Order { get; }

    public int BaseScore { get; }

    protected override int OwnScore => BaseScore;

    public override void Accept(IMissionComponentVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var child in Children)
            child.Accept(visitor);
    }
}
