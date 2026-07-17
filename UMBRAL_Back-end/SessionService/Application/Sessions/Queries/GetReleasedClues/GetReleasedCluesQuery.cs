namespace SessionService.Application.Sessions.Queries.GetReleasedClues;

using MediatR;
using SessionService.Domain.Common;

public record GetReleasedCluesQuery(Guid SessionId, Guid TeamId)
    : IRequest<Result<ReleasedCluesDto>>;
