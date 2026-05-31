namespace SessionService.Application.Missions.Composite;

/// <summary>
/// COMPOSITE PATTERN — base for every component.
///
/// Provides the default "leaf" behaviour: no children and unsupported
/// <see cref="Add"/>/<see cref="Remove"/>. <see cref="CompositeMissionComponent"/>
/// overrides this to hold children; leaves (e.g. <see cref="ClueComponent"/>)
/// inherit the defaults as-is.
/// </summary>
public abstract class MissionComponentBase : IMissionComponent
{
    protected MissionComponentBase(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; }

    public string Name { get; }

    public abstract string ComponentType { get; }

    public virtual IReadOnlyList<IMissionComponent> Children => [];

    /// <summary>Score this node contributes on its own (excluding children). 0 by default.</summary>
    protected virtual int OwnScore => 0;

    public virtual void Add(IMissionComponent child)
        => throw new NotSupportedException($"A '{ComponentType}' node is a leaf and cannot have children.");

    public virtual void Remove(IMissionComponent child)
        => throw new NotSupportedException($"A '{ComponentType}' node is a leaf and cannot have children.");

    // Leaf default: a node with no children scores only its own value.
    public virtual int TotalScore() => OwnScore;

    public abstract void Accept(IMissionComponentVisitor visitor);
}
