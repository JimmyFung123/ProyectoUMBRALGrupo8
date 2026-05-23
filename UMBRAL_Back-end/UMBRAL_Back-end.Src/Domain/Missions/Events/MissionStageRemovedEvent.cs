namespace UMBRAL_Back_end.Domain.Missions.Events;

using MediatR;

public record MissionStageRemovedEvent(
    Guid MissionId,
    Guid StageId,
    DateTime OccurredAt
) : INotification;
