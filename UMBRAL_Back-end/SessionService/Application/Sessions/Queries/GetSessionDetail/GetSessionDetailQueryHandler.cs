namespace SessionService.Application.Sessions.Queries.GetSessionDetail;

using MediatR;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

public class GetSessionDetailQueryHandler
    : IRequestHandler<GetSessionDetailQuery, Result<SessionDetailDto>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamRepository _teamRepository;

    public GetSessionDetailQueryHandler(
        ISessionRepository sessionRepository,
        ITeamRepository teamRepository)
    {
        _sessionRepository = sessionRepository;
        _teamRepository = teamRepository;
    }

    public async Task<Result<SessionDetailDto>> Handle(
        GetSessionDetailQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<SessionDetailDto>(SessionErrors.NotFound);

        var teams = await _teamRepository.GetBySessionIdAsync(request.SessionId, cancellationToken);

        var dto = new SessionDetailDto(
            session.Id,
            session.MissionId,
            session.Name,
            session.Status.ToString(),
            session.CreatedAt,
            session.ScheduledAt,
            teams.Select(t => new TeamDto(
                t.Id,
                t.Name,
                t.IsConnected,
                t.CurrentStageOrder,
                t.CluesReceivedCurrentStage,
                t.TotalCluesReceived)).ToList());

        return Result.Success(dto);
    }
}
