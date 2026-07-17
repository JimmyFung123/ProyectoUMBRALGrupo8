namespace UMBRAL.Contracts.Events;
public record StageAddedIntegrationEvent(Guid StageId, Guid MissionId, string StageType, DateTime OccurredAt);
