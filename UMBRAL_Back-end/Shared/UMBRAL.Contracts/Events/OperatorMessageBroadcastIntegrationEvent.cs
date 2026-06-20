namespace UMBRAL.Contracts.Events;

/// <summary>
/// Published when an operator broadcasts a message to participants (HU-28).
/// OperatorMessageBroadcastConsumer relays it to ISessionNotifier asynchronously.
/// </summary>
public record OperatorMessageBroadcastIntegrationEvent(
    Guid SessionId, string Message, string ActorName, DateTime DeliveredAt);
