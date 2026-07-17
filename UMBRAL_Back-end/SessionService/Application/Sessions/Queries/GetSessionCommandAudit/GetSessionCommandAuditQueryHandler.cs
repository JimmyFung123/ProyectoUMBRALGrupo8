namespace SessionService.Application.Sessions.Queries.GetSessionCommandAudit;

using MediatR;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

/// <summary>
/// HU-26 handler — projects the immutable <see cref="SessionEvent"/> log into a
/// DTO ready for the technical audit screen.
///
/// Reads the same table HU-22 uses; the difference is purely in the projection:
/// here every entry carries <c>CommandType</c> and <c>Outcome</c> so the UI can
/// filter and export the trail with full forensic detail.
/// </summary>
public class GetSessionCommandAuditQueryHandler
    : IRequestHandler<GetSessionCommandAuditQuery, Result<SessionCommandAuditDto>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionEventRepository _eventRepository;

    public GetSessionCommandAuditQueryHandler(
        ISessionRepository sessionRepository,
        ISessionEventRepository eventRepository)
    {
        _sessionRepository = sessionRepository;
        _eventRepository = eventRepository;
    }

    public async Task<Result<SessionCommandAuditDto>> Handle(
        GetSessionCommandAuditQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<SessionCommandAuditDto>(SessionErrors.NotFound);

        var events = await _eventRepository.GetAllBySessionIdAsync(request.SessionId, cancellationToken);

        var entries = events
            .Select(e => new SessionCommandAuditEntryDto(
                Id: e.Id,
                OccurredAt: e.OccurredAt,
                ActorName: e.ActorName,
                CommandType: e.CommandType,
                Outcome: e.Outcome,
                Description: e.Description))
            .ToList();

        return Result.Success(new SessionCommandAuditDto(
            SessionId: session.Id,
            SessionStatus: session.Status.ToString(),
            GeneratedAt: DateTime.UtcNow,
            Entries: entries));
    }
}
