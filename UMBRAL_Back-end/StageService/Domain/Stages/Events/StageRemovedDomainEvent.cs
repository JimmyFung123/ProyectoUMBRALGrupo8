namespace StageService.Domain.Stages.Events;

using StageService.Domain.Common;

public record StageRemovedDomainEvent(
    Guid StageId,
    Guid MissionId,
    DateTime OccurredAt
) : IDomainEvent;
