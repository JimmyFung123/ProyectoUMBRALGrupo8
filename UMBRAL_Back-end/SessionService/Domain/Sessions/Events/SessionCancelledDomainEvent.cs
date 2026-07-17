namespace SessionService.Domain.Sessions.Events;

using SessionService.Domain.Common;

public record SessionCancelledDomainEvent(
    Guid SessionId,
    string? OperatorName,
    DateTime OccurredAt
) : IDomainEvent;
