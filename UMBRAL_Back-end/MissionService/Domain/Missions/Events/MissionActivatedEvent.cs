namespace UMBRAL_Back_end.Domain.Missions.Events;

using UMBRAL_Back_end.Domain.Common;

public record MissionActivatedEvent(
    Guid MissionId,
    string Name,
    int StageCount,
    DateTime ActivatedAt
) : IDomainEvent;
