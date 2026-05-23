namespace UMBRAL.Contracts.Events;

/// <summary>
/// Published by MissionService when a mission transitions to Inactive.
/// SessionService consumes this to prevent new session creation for that mission.
/// </summary>
public record MissionDeactivatedIntegrationEvent(
    Guid MissionId,
    string Name,
    DateTime OccurredAt);
