namespace UMBRAL_Back_end.Domain.Missions.Events;

using UMBRAL_Back_end.Domain.Common;

public record MissionCreatedDomainEvent(
    Guid MissionId,
    string Name,
    MissionStatus Status,
    DateTime CreatedAt
) : IDomainEvent;
