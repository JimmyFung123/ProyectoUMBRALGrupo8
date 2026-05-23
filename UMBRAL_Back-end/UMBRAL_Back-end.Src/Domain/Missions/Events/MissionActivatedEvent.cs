namespace UMBRAL_Back_end.Domain.Missions.Events;

using MediatR;

public record MissionActivatedEvent(
    Guid MissionId,
    string Name,
    int StageCount,
    DateTime ActivatedAt
) : INotification;
