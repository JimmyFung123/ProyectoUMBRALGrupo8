namespace SessionService.Application.Sessions.Queries.GetSessionAudit;

/// <summary>
/// Single entry of the session audit timeline (HU-22).
/// Carries everything required by criterion 1: event description, exact
/// timestamp and the actor (operator name or "Sistema").
/// </summary>
public record SessionAuditEntryDto(
    Guid Id,
    string Description,
    string ActorName,
    DateTime OccurredAt);

/// <summary>
/// Full audit response. <see cref="SessionStatus"/> is included so the UI
/// knows whether to render the empty-state message demanded by HU-22's
/// alternate flow ("Aún no hay eventos registrados para esta sesión" when
/// the session is still in preparation or was cancelled before starting).
/// </summary>
public record SessionAuditDto(
    Guid SessionId,
    string SessionStatus,
    IReadOnlyList<SessionAuditEntryDto> Entries);
