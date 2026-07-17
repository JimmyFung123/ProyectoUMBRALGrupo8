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
///
/// "Safe Composite" variant: child-mutation (<c>Add</c>/<c>Remove</c>) is NOT on
/// this shared abstraction, because leaves cannot honour it. The supported
/// operation lives on <see cref="CompositeMissionComponent"/>; leaves keep only a
/// throwing default (<see cref="MissionComponentBase"/>). So every member declared
/// here is fully substitutable for any node (none throws for some subtype) — LSP-safe.
/// </summary>
public interface IMissionComponent
{
    Guid Id { get; }

    string Name { get; }

    /// <summary>"Mission" | "Stage" | "Clue" — discriminates the node kind.</summary>
    string ComponentType { get; }

    /// <summary>Child components. Empty for leaves (clues).</summary>
    IReadOnlyList<IMissionComponent> Children { get; }

    /// <summary>Recursive aggregate score of this node and its whole sub-tree.</summary>
    int TotalScore();

    /// <summary>Double-dispatch entry point for the Visitor pattern.</summary>
    void Accept(IMissionComponentVisitor visitor);
}
