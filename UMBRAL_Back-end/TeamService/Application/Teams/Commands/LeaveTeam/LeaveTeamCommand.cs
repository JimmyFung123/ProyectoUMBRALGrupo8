namespace TeamService.Application.Teams.Commands.LeaveTeam;
using MediatR;
using TeamService.Domain.Common;

/// <summary>Result value is true when the team was left empty and got deleted.</summary>
public record LeaveTeamCommand(Guid TeamId) : IRequest<Result<bool>>;
