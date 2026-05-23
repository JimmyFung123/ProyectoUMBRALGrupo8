namespace UMBRAL_Back_end.Domain.Missions.Events;

using MediatR;

public record TriviaStageConfigure(
    Guid MissionId,
    Guid StageId,
    string Question,
    int OptionCount,
    DateTime OccurredAt
) : INotification;
