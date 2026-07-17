namespace SessionService.Domain.Sessions.Events;

using SessionService.Domain.Common;

public record SessionFinalizedDomainEvent(
    Guid SessionId,
    SessionStatus Status,
    string? OperatorName,
    DateTime OccurredAt
) : IDomainEvent;
