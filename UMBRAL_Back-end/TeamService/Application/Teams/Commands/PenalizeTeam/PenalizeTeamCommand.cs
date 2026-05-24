namespace TeamService.Application.Teams.Commands.PenalizeTeam;

using MediatR;
using TeamService.Domain.Common;

public record PenalizeTeamCommand(Guid TeamId, int Points, string Reason) : IRequest<Result<int>>;
