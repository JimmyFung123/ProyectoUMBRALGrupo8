namespace SessionService.Domain.Sessions.Events;

using SessionService.Domain.Common;

public record SessionCreatedDomainEvent(
    Guid SessionId,
    string Name,
    string? OperatorName,
    DateTime OccurredAt
) : IDomainEvent;
