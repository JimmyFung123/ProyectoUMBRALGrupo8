namespace TeamService.Application.Teams.Queries.GetSessionRanking;

using MediatR;

public record GetSessionRankingQuery(Guid SessionId) : IRequest<SessionRankingDto>;
