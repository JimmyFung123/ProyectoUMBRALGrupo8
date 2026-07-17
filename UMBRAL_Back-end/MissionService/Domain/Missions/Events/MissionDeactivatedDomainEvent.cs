namespace UMBRAL_Back_end.Domain.Missions.Events;

using UMBRAL_Back_end.Domain.Common;

public record MissionDeactivatedDomainEvent(
    Guid MissionId,
    string Name,
    DateTime OccurredAt
) : IDomainEvent;
