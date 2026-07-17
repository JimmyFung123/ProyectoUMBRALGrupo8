namespace UMBRAL_Back_end.Domain.Missions.Events;

using UMBRAL_Back_end.Domain.Common;

public record MissionUpdatedDomainEvent(
    Guid MissionId,
    string Name,
    DifficultyLevel Difficulty,
    DateTime OccurredAt
) : IDomainEvent;
