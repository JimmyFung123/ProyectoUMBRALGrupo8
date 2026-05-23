namespace UMBRAL_Back_end.Domain.Missions.Events;

using MediatR;

public record MissionUpdatedEvent(
    Guid MissionId,
    string Name,
    string Difficulty,
    int MaxDuration,
    DateTime UpdatedAt
) : INotification;
