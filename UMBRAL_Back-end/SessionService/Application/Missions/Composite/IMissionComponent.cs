namespace SessionService.Application.Missions.Composite;

/// <summary>
/// COMPOSITE PATTERN — "Component".
///
/// Common interface for every node of a mission's structure tree:
///   Mission (root composite) → Stages (composites) → Clues (leaves).
///
/// Clients can treat an individual clue and a whole mission sub-tree
/// uniformly (e.g. ask any node for its <see cref="TotalScore"/> or walk it
/// with an <see cref="IMissionComponentVisitor"/>), without knowing whether
/// the node is a leaf or a composite.
/// </summary>
public interface IMissionComponent
{
    Guid Id { get; }

    string Name { get; }

    /// <summary>"Mission" | "Stage" | "Clue" — discriminates the node kind.</summary>
    string ComponentType { get; }

    /// <summary>Child components. Empty for leaves (clues).</summary>
    IReadOnlyList<IMissionComponent> Children { get; }

    /// <summary>Composite operation: add a child. Leaves throw <see cref="NotSupportedException"/>.</summary>
    void Add(IMissionComponent child);

    /// <summary>Composite operation: remove a child. Leaves throw <see cref="NotSupportedException"/>.</summary>
    void Remove(IMissionComponent child);

    /// <summary>Recursive aggregate score of this node and its whole sub-tree.</summary>
    int TotalScore();

    /// <summary>Double-dispatch entry point for the Visitor pattern.</summary>
    void Accept(IMissionComponentVisitor visitor);
}
