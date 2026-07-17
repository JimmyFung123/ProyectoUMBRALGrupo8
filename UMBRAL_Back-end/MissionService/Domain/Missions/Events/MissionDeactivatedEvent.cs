namespace UMBRAL_Back_end.Domain.Missions.Events;

using UMBRAL_Back_end.Domain.Common;

public record MissionDeactivatedEvent(
    Guid MissionId,
    string Name,
    DateTime DeactivatedAt
) : IDomainEvent;
