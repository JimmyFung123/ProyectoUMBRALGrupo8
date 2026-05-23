namespace UMBRAL.Contracts.Events;

/// <summary>
/// Published by MissionService when a new mission is created (Status = Inactive).
/// SessionService consumes this to seed its MissionLookup read-side replica.
/// </summary>
public record MissionCreatedIntegrationEvent(
    Guid MissionId,
    string Name,
    string Status,
    DateTime OccurredAt);
