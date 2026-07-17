namespace SessionService.Domain.Sessions.Events;

using SessionService.Domain.Common;

public record SessionUpdatedDomainEvent(
    Guid SessionId,
    string Name,
    DateTime? ScheduledAt,
    string? OperatorName,
    DateTime OccurredAt
) : IDomainEvent;
