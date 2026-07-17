namespace UMBRAL_Back_end.Domain.Missions.Events;

using UMBRAL_Back_end.Domain.Common;

public record MissionUpdatedEvent(
    Guid MissionId,
    string Name,
    string Difficulty,
    int MaxDuration,
    DateTime UpdatedAt
) : IDomainEvent;
