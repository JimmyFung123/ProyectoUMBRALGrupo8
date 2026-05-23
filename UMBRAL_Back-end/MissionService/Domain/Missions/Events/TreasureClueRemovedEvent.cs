namespace UMBRAL_Back_end.Domain.Missions.Events;

using MediatR;

public record TreasureClueRemovedEvent(
    Guid MissionId,
    Guid StageId,
    Guid ClueId,
    DateTime OccurredAt
) : INotification;
