namespace TeamService.Application.Teams.Commands.ForceAdvance;

using MediatR;
using TeamService.Application.Rankings;
using TeamService.Domain.Common;
using TeamService.Domain.Teams;

public class ForceAdvanceTeamCommandHandler : IRequestHandler<ForceAdvanceTeamCommand, Result<StageTransitionOutcome>>
{
    private readonly ITeamRepository _repo;
    private readonly IRankingProjector _rankingProjector;

    public ForceAdvanceTeamCommandHandler(ITeamRepository repo, IRankingProjector rankingProjector)
    {
        _repo = repo;
        _rankingProjector = rankingProjector;
    }

    public async Task<Result<StageTransitionOutcome>> Handle(ForceAdvanceTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _repo.GetByIdAsync(request.TeamId, cancellationToken);
        if (team is null) return Result.Failure<StageTransitionOutcome>(TeamErrors.NotFound);

        var result = team.ForceAdvance(request.NextStageOrder);
        if (result.IsFailure) return result;

        await _repo.SaveChangesAsync(cancellationToken);

        // HU-24: refresh the projection so CurrentStageOrder shows the new stage.
        // Done AFTER SaveChanges so a projector failure never blocks the advance.
        try { await _rankingProjector.RebuildAsync(team.SessionId, cancellationToken); }
        catch { /* projection is best-effort — advance already persisted */ }

        return result;
    }
}
