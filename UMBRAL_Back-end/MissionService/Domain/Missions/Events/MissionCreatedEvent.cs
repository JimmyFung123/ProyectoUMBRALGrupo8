namespace UMBRAL_Back_end.Domain.Missions.Events;

using MediatR;

/// <summary>
/// Published via RabbitMQ (MassTransit) after a mission is created successfully.
/// Consumers: notification service, audit log, analytics.
/// </summary>
public record MissionCreatedEvent(
    Guid MissionId,
    string Name,
    string Difficulty,
    int MaxDuration,
    DateTime CreatedAt
) : INotification;
