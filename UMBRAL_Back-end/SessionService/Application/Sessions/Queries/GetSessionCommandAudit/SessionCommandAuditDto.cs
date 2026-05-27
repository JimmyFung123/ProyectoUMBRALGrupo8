namespace SessionService.Application.Sessions.Queries.GetSessionCommandAudit;

/// <summary>
/// Single row of the technical command audit log (HU-26).
/// Carries the metadata required by criterion 1 — actor, command type, outcome
/// and the millisecond-precise UTC timestamp.
/// </summary>
public record SessionCommandAuditEntryDto(
    Guid Id,
    DateTime OccurredAt,
    string ActorName,
    string? CommandType,
    string? Outcome,
    string Description);

/// <summary>
/// Full command audit response (HU-26). <see cref="SessionStatus"/> mirrors
/// HU-22 so the UI can render a friendly empty state for pending or cancelled
/// sessions.
/// </summary>
public record SessionCommandAuditDto(
    Guid SessionId,
    string SessionStatus,
    DateTime GeneratedAt,
    IReadOnlyList<SessionCommandAuditEntryDto> Entries);
