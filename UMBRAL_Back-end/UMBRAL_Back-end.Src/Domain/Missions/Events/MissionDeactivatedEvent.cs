namespace UMBRAL_Back_end.Domain.Missions.Events;

using MediatR;

public record MissionDeactivatedEvent(
    Guid MissionId,
    string Name,
    DateTime DeactivatedAt
) : INotification;
