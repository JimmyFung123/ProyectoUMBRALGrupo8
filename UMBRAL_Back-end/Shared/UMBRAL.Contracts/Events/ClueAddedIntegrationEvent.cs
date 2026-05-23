namespace UMBRAL.Contracts.Events;
public record ClueAddedIntegrationEvent(Guid ClueId, Guid StageId, Guid MissionId, string Content, DateTime OccurredAt);
