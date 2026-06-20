namespace UMBRAL.Contracts.Events;

/// <summary>
/// Published by SessionService command handlers whenever an operator action
/// must be recorded on the session's audit trail. SessionAuditConsumer persists
/// the corresponding SessionEvent asynchronously.
/// </summary>
public record SessionAuditIntegrationEvent(
    Guid SessionId,
    string Description,
    string? ActorName,
    string? CommandType,
    string? Outcome,
    DateTime OccurredAt);
