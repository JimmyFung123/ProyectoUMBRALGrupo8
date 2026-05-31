namespace SessionService.Application.Missions.Composite;

/// <summary>
/// COMPOSITE PATTERN — "Leaf". Wraps a Clue. A clue has no children and
/// contributes no score of its own; it inherits the leaf defaults from
/// <see cref="MissionComponentBase"/> (Add/Remove throw, TotalScore = 0).
/// </summary>
public sealed class ClueComponent : MissionComponentBase
{
    public ClueComponent(Guid id, int order, string? content)
        : base(id, content is { Length: > 0 } ? content : $"Pista {order}")
    {
        Order = order;
        Content = content;
    }

    public override string ComponentType => "Clue";

    public int Order { get; }

    public string? Content { get; }

    public override void Accept(IMissionComponentVisitor visitor) => visitor.Visit(this);
}
