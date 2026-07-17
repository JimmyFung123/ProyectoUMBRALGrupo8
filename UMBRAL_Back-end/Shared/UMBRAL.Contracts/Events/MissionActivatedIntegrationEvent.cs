namespace UMBRAL.Contracts.Events;

/// <summary>
/// Published by MissionService when a mission transitions to Active.
/// SessionService consumes this to allow session creation for that mission.
/// </summary>
public record MissionActivatedIntegrationEvent(
    Guid MissionId,
    string Name,
    DateTime OccurredAt,
    string Difficulty = "Medium");
