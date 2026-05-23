namespace UMBRAL.Contracts.Events;
public record ClueRemovedIntegrationEvent(Guid ClueId, Guid StageId, Guid MissionId, DateTime OccurredAt);
