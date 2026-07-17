namespace SessionService.Application.Sessions.Queries.GetSessionByCode;
using MediatR;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

public class GetSessionByCodeQueryHandler : IRequestHandler<GetSessionByCodeQuery, Result<SessionByCodeDto>>
{
    private readonly ISessionRepository _repo;
    public GetSessionByCodeQueryHandler(ISessionRepository repo) => _repo = repo;

    public async Task<Result<SessionByCodeDto>> Handle(GetSessionByCodeQuery request, CancellationToken cancellationToken)
    {
        var session = await _repo.GetByAccessCodeAsync(request.Code, cancellationToken);
        if (session is null)
            return Result.Failure<SessionByCodeDto>(SessionErrors.NotFound);

        return Result.Success(new SessionByCodeDto(
            session.Id, session.Name, session.Status.ToString(), session.AccessCode, session.MissionId));
    }
}
