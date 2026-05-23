namespace UMBRAL_Back_end.Domain.Missions.Events;

using MediatR;

public record TreasureStageConfigure(
    Guid MissionId,
    Guid StageId,
    double Latitude,
    double Longitude,
    DateTime OccurredAt
) : INotification;
