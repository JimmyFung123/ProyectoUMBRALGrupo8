namespace StageService.Domain.Stages.Events;

using StageService.Domain.Common;

public record StageAddedDomainEvent(
    Guid StageId,
    Guid MissionId,
    StageType Type,
    DateTime OccurredAt
) : IDomainEvent;
