namespace SessionService.Application.Missions.Composite;

/// <summary>
/// COMPOSITE PATTERN — root composite. Wraps a Mission and holds its
/// <see cref="StageComponent"/> children. Its own score is 0; the mission's
/// total possible score is the sum of its stages' scores.
/// </summary>
public sealed class MissionComponent : CompositeMissionComponent
{
    public MissionComponent(Guid id, string name, string difficulty, string status)
        : base(id, name)
    {
        Difficulty = difficulty;
        Status = status;
    }

    public override string ComponentType => "Mission";

    /// <summary>"Easy" | "Medium" | "Hard".</summary>
    public string Difficulty { get; }

    /// <summary>"Active" | "Inactive".</summary>
    public string Status { get; }

    public override void Accept(IMissionComponentVisitor visitor)
    {
        visitor.Visit(this);
        foreach (var child in Children)
            child.Accept(visitor);
    }
}
