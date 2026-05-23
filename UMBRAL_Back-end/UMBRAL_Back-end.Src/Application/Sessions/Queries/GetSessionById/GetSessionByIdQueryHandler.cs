namespace UMBRAL_Back_end.Application.Sessions.Queries.GetSessionById;

using MediatR;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Sessions;

public class GetSessionByIdQueryHandler : IRequestHandler<GetSessionByIdQuery, Result<SessionDetailDto>>
{
    private readonly ISessionRepository _repository;

    public GetSessionByIdQueryHandler(ISessionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<SessionDetailDto>> Handle(GetSessionByIdQuery request, CancellationToken cancellationToken)
    {
        var session = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (session is null)
            return Result.Failure<SessionDetailDto>(SessionErrors.NotFound);

        return Result.Success(
            new SessionDetailDto(session.Id, session.MissionId, session.Name, session.Status.ToString(), session.CreatedAt, session.ScheduledAt));
    }
}
