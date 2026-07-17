namespace SessionService.Application.Sessions.Queries.GetSessionAudit;

using MediatR;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

/// <summary>
/// Returns the full audit trail of a session, chronologically (HU-22).
///
/// Read-only handler — separated from the dashboard "recent events" query so
/// each caller picks the right shape: the dashboard pages the last 20, the
/// audit screen consumes the full timeline.
/// </summary>
public class GetSessionAuditQueryHandler
    : IRequestHandler<GetSessionAuditQuery, Result<SessionAuditDto>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionEventRepository _eventRepository;

    public GetSessionAuditQueryHandler(
        ISessionRepository sessionRepository,
        ISessionEventRepository eventRepository)
    {
        _sessionRepository = sessionRepository;
        _eventRepository = eventRepository;
    }

    public async Task<Result<SessionAuditDto>> Handle(
        GetSessionAuditQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<SessionAuditDto>(SessionErrors.NotFound);

        var events = await _eventRepository.GetAllBySessionIdAsync(request.SessionId, cancellationToken);

        var entries = events
            .Select(e => new SessionAuditEntryDto(e.Id, e.Description, e.ActorName, e.OccurredAt))
            .ToList();

        return Result.Success(new SessionAuditDto(
            SessionId: session.Id,
            SessionStatus: session.Status.ToString(),
            Entries: entries));
    }
}
