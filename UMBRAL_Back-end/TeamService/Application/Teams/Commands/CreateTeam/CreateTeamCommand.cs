namespace TeamService.Application.Teams.Commands.CreateTeam;
using MediatR;
using TeamService.Domain.Common;
public record CreateTeamCommand(Guid SessionId, string TeamName) : IRequest<Result<CreateTeamResult>>;
public record CreateTeamResult(Guid TeamId, string InviteCode);
