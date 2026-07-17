namespace TeamService.Application.Teams.Commands.RecordWrongAttempt;

using MediatR;
using TeamService.Application.Rankings;
using TeamService.Domain.Common;
using TeamService.Domain.Teams;

public class RecordWrongAttemptCommandHandler : IRequestHandler<RecordWrongAttemptCommand, Result<WrongAttemptOutcome>>
{
    private readonly ITeamRepository _repo;
    private readonly IRankingProjector _rankingProjector;

    public RecordWrongAttemptCommandHandler(ITeamRepository repo, IRankingProjector rankingProjector)
    {
        _repo = repo;
        _rankingProjector = rankingProjector;
    }

    public async Task<Result<WrongAttemptOutcome>> Handle(RecordWrongAttemptCommand request, CancellationToken cancellationToken)
    {
        var team = await _repo.GetByIdAsync(request.TeamId, cancellationToken);
        if (team is null)
            return Result.Failure<WrongAttemptOutcome>(TeamErrors.NotFound);

        var outcome = team.RecordWrongAttempt(request.BlockedOptionId, request.ScorePenalty);

        await _rankingProjector.RebuildAsync(team.SessionId, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        return Result.Success(outcome);
    }
}
