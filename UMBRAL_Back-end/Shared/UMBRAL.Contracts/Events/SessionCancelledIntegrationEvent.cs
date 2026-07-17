namespace UMBRAL.Contracts.Events;

/// <summary>
/// Published by SessionService when a session is cancelled.
/// TeamService consumes this to delete all teams enrolled in the cancelled session.
/// </summary>
public record SessionCancelledIntegrationEvent(Guid SessionId, DateTime OccurredAt);
