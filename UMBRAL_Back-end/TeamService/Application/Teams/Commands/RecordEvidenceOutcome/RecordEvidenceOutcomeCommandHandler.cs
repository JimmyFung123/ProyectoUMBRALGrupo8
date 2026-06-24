namespace TeamService.Application.Teams.Commands.RecordEvidenceOutcome;

using MediatR;
using TeamService.Application.Rankings;
using TeamService.Domain.Common;
using TeamService.Domain.Teams;

public class RecordEvidenceOutcomeCommandHandler : IRequestHandler<RecordEvidenceOutcomeCommand, Result<StageTransitionOutcome>>
{
    private readonly ITeamRepository _repo;
    private readonly IRankingProjector _rankingProjector;

    public RecordEvidenceOutcomeCommandHandler(ITeamRepository repo, IRankingProjector rankingProjector)
    {
        _repo = repo;
        _rankingProjector = rankingProjector;
    }

    public async Task<Result<StageTransitionOutcome>> Handle(RecordEvidenceOutcomeCommand request, CancellationToken cancellationToken)
    {
        var team = await _repo.GetByIdAsync(request.TeamId, cancellationToken);
        if (team is null)
            return Result.Failure<StageTransitionOutcome>(TeamErrors.NotFound);

        var result = team.RecordEvidenceOutcome(request.IsCorrect, request.ScoreChange, request.NextStageOrder);

        // HU-24: refresh the ranking projection (Score + LastStageCompletedAt changed).
        await _rankingProjector.RebuildAsync(team.SessionId, cancellationToken);

        await _repo.SaveChangesAsync(cancellationToken);
        return result;
    }
}
