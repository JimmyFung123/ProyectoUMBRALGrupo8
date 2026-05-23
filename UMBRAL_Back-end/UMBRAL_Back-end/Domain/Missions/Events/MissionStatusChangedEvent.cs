namespace UMBRAL_Back_end.Domain.Missions.Events;

using MediatR;

/// <summary>
/// Published via RabbitMQ (MassTransit) after a mission status changes.
/// Consumers: session service (to prevent new bookings on inactive missions), audit log.
/// </summary>
public record MissionStatusChangedEvent(
    Guid MissionId,
    string NewStatus,
    DateTime ChangedAt
) : INotification;
