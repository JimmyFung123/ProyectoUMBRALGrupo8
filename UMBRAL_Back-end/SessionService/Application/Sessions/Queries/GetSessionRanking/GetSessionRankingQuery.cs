namespace SessionService.Application.Sessions.Queries.GetSessionRanking;

using MediatR;
using SessionService.Domain.Common;

public record GetSessionRankingQuery(Guid SessionId) : IRequest<Result<SessionRankingResponse>>;
