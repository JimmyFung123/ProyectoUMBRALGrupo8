namespace UMBRAL.Contracts.Events;
public record StageRemovedIntegrationEvent(Guid StageId, Guid MissionId, DateTime OccurredAt);
