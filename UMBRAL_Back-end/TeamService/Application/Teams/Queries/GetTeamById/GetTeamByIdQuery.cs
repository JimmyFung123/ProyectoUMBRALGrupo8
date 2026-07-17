namespace TeamService.Application.Teams.Queries.GetTeamById;

using MediatR;
using TeamService.Domain.Common;

public record GetTeamByIdQuery(Guid TeamId) : IRequest<Result<TeamInfoDto>>;
