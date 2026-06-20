namespace UMBRAL.Contracts.Events;

/// <summary>
/// Published whenever an operator penalizes a team. TeamPenalizedConsumer
/// relays it to ISessionNotifier asynchronously.
/// </summary>
public record TeamPenalizedIntegrationEvent(
    Guid SessionId, Guid TeamId, string TeamName,
    int Points, string Reason, int NewScore, string ActorName);
