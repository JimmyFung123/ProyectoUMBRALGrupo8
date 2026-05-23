namespace UMBRAL_Back_end.Domain.Missions.Events;

using MediatR;

public record TreasureClueAddedEvent(
    Guid MissionId,
    Guid StageId,
    Guid ClueId,
    int Order,
    DateTime OccurredAt
) : INotification;
