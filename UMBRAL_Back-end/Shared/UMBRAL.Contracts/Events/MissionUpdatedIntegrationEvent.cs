namespace UMBRAL.Contracts.Events;

/// <summary>
/// Published by MissionService when a mission's properties are updated.
/// SessionService consumes this to keep its MissionLookup replica in sync,
/// particularly the Difficulty field used by IScoringStrategy.
/// </summary>
public record MissionUpdatedIntegrationEvent(
    Guid MissionId,
    string Name,
    string Difficulty,
    DateTime OccurredAt);
