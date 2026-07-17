namespace SessionService.Application.Sessions.Queries.GetSessionDetail;

using MediatR;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

public class GetSessionDetailQueryHandler
    : IRequestHandler<GetSessionDetailQuery, Result<SessionDetailDto>>
{
    private readonly ISessionRepository _sessionRepository;

    public GetSessionDetailQueryHandler(ISessionRepository sessionRepository)
        => _sessionRepository = sessionRepository;

    public async Task<Result<SessionDetailDto>> Handle(
        GetSessionDetailQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<SessionDetailDto>(SessionErrors.NotFound);

        var dto = new SessionDetailDto(
            session.Id,
            session.MissionId,
            session.Name,
            session.Status.ToString(),
            session.CreatedAt,
            session.ScheduledAt,
            session.AccessCode);

        return Result.Success(dto);
    }
}
