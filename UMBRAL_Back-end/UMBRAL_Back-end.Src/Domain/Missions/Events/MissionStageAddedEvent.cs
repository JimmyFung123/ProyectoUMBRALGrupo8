namespace UMBRAL_Back_end.Domain.Missions.Events;

using MediatR;

public record MissionStageAddedEvent(
    Guid MissionId,
    Guid StageId,
    string Title,
    string StageType,
    int Order,
    DateTime OccurredAt
) : INotification;
