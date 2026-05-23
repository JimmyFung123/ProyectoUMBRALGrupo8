namespace UMBRAL_Back_end.Domain.Missions.Events;

using MediatR;

public record TreasureClueReorderedEvent(
    Guid MissionId,
    Guid StageId,
    Guid ClueId,
    int NewOrder,
    DateTime OccurredAt
) : INotification;
