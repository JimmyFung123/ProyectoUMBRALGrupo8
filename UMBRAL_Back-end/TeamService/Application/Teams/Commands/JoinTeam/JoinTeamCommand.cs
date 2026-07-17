namespace TeamService.Application.Teams.Commands.JoinTeam;
using MediatR;
using TeamService.Domain.Common;
public record JoinTeamCommand(string InviteCode) : IRequest<Result<JoinTeamResult>>;
public record JoinTeamResult(Guid TeamId, string TeamName, string InviteCode, int MemberCount);
