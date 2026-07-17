namespace UMBRAL.Contracts.Events;

/// <summary>
/// Published whenever a clue is released to a team (manual or automatic).
/// ClueReleasedConsumer relays it to ISessionNotifier asynchronously.
/// </summary>
public record ClueReleasedIntegrationEvent(
    Guid SessionId, Guid TeamId, string? Content,
    double? Latitude, double? Longitude, int? RadiusMeters,
    int ClueNumber, bool IsAutomatic);
