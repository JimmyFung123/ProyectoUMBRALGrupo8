namespace TeamService.Application.Teams.Commands.ForceAdvance;

using MediatR;
using TeamService.Domain.Common;

public record ForceAdvanceTeamCommand(Guid TeamId, int NextStageOrder) : IRequest<Result<bool>>;
