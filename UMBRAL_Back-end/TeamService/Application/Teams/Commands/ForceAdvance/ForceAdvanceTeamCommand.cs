namespace TeamService.Application.Teams.Commands.ForceAdvance;

using MediatR;
using TeamService.Domain.Common;
using TeamService.Domain.Teams;

public record ForceAdvanceTeamCommand(Guid TeamId, int NextStageOrder)
    : IRequest<Result<StageTransitionOutcome>>;
