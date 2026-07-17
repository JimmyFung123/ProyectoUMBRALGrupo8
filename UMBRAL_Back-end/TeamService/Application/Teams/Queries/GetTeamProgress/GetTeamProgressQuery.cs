namespace TeamService.Application.Teams.Queries.GetTeamProgress;

using MediatR;

public record GetTeamProgressQuery(Guid SessionId) : IRequest<IReadOnlyList<TeamProgressDto>>;
