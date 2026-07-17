namespace UMBRAL.Contracts.Events;

/// <summary>
/// Published whenever a session transitions state (start, pause, resume,
/// finalize, force-advance). SessionStateChangedConsumer relays it to
/// ISessionNotifier so a transient SignalR failure never fails the command
/// that already succeeded.
/// </summary>
public record SessionStateChangedIntegrationEvent(Guid SessionId, string? NewStatus);
