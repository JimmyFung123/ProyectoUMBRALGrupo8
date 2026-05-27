namespace SessionService.Application.Sessions.Queries.GetSessionCommandAudit;

using MediatR;
using SessionService.Domain.Common;

/// <summary>
/// HU-26 — technical command audit log for a session.
/// Returns every command executed against the session with millisecond-precise
/// timestamp, command type and outcome. Distinct from <c>GetSessionAuditQuery</c>
/// (HU-22), which produces a human-readable timeline.
/// </summary>
public record GetSessionCommandAuditQuery(Guid SessionId)
    : IRequest<Result<SessionCommandAuditDto>>;
