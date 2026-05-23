namespace UMBRAL_Back_end.Domain.Sessions.Events;

using MediatR;

public record SessionCreatedEvent(
    Guid SessionId,
    Guid MissionId,
    string Name,
    DateTime OccurredAt) : INotification;
