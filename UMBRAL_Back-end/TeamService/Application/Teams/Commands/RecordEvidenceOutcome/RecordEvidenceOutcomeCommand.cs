namespace TeamService.Application.Teams.Commands.RecordEvidenceOutcome;

using MediatR;
using TeamService.Domain.Common;
using TeamService.Domain.Teams;

public record RecordEvidenceOutcomeCommand(
    Guid TeamId,
    bool IsCorrect,
    int ScoreChange,
    int NextStageOrder) : IRequest<Result<StageTransitionOutcome>>;
