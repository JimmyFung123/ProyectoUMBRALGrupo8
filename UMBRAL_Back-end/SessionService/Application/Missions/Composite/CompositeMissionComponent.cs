namespace SessionService.Application.Missions.Composite;

/// <summary>
/// COMPOSITE PATTERN — "Composite".
///
/// A node that owns child components and aggregates over them. Both
/// <see cref="MissionComponent"/> and <see cref="StageComponent"/> derive from
/// this, so the part-whole recursion (a mission's score is the sum of its
/// stages' scores, a stage's score is its own plus its clues') lives in one place.
/// </summary>
public abstract class CompositeMissionComponent : MissionComponentBase
{
    private readonly List<IMissionComponent> _children = [];

    protected CompositeMissionComponent(Guid id, string name) : base(id, name) { }

    public override IReadOnlyList<IMissionComponent> Children => _children;

    public override void Add(IMissionComponent child) => _children.Add(child);

    public override void Remove(IMissionComponent child) => _children.Remove(child);

    // Composite: own contribution plus the whole sub-tree, recursively.
    public override int TotalScore() => OwnScore + _children.Sum(c => c.TotalScore());
}
