namespace ClueService.Domain.Clues.Events;

using ClueService.Domain.Common;

public record ClueRemovedDomainEvent(
    Guid ClueId,
    Guid StageId,
    Guid MissionId,
    DateTime OccurredAt
) : IDomainEvent;
