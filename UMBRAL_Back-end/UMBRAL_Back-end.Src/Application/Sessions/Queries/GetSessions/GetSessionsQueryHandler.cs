namespace UMBRAL_Back_end.Application.Sessions.Queries.GetSessions;

using MediatR;
using UMBRAL_Back_end.Domain.Sessions;

public class GetSessionsQueryHandler : IRequestHandler<GetSessionsQuery, IReadOnlyList<SessionDto>>
{
    private readonly ISessionRepository _repository;

    public GetSessionsQueryHandler(ISessionRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<SessionDto>> Handle(GetSessionsQuery request, CancellationToken cancellationToken)
    {
        SessionStatus? status = null;
        if (request.Status is not null && Enum.TryParse<SessionStatus>(request.Status, ignoreCase: true, out var parsed))
            status = parsed;

        var sessions = await _repository.GetAllAsync(request.MissionId, status, cancellationToken);

        return sessions
            .Select(s => new SessionDto(s.Id, s.MissionId, s.Name, s.Status.ToString(), s.CreatedAt, s.ScheduledAt))
            .ToList();
    }
}
