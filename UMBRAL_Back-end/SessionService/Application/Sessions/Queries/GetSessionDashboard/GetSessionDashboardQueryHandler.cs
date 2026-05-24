namespace SessionService.Application.Sessions.Queries.GetSessionDashboard;

using MediatR;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

public class GetSessionDashboardQueryHandler
    : IRequestHandler<GetSessionDashboardQuery, Result<SessionDashboardDto>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionEventRepository _eventRepository;

    public GetSessionDashboardQueryHandler(
        ISessionRepository sessionRepository,
        ISessionEventRepository eventRepository)
    {
        _sessionRepository = sessionRepository;
        _eventRepository = eventRepository;
    }

    public async Task<Result<SessionDashboardDto>> Handle(
        GetSessionDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<SessionDashboardDto>(SessionErrors.NotFound);

        var events = await _eventRepository.GetRecentBySessionIdAsync(
            request.SessionId, maxCount: 20, cancellationToken);

        var dto = new SessionDashboardDto(
            Id: session.Id,
            Name: session.Name,
            Status: session.Status.ToString(),
            CreatedAt: session.CreatedAt,
            ScheduledAt: session.ScheduledAt,
            RecentEvents: events
                .Select(e => new SessionEventDto(e.Id, e.Description, e.OccurredAt))
                .ToList());

        return Result.Success(dto);
    }
}
