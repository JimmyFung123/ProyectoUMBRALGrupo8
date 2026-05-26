namespace TeamService.Application.Teams.Commands.AnswerTrivia;

using MediatR;
using TeamService.Application.Rankings;
using TeamService.Domain.Common;
using TeamService.Domain.Teams;

public class AnswerTriviaCommandHandler : IRequestHandler<AnswerTriviaCommand, Result<int>>
{
    private readonly ITeamRepository _repo;
    private readonly IRankingProjector _rankingProjector;

    public AnswerTriviaCommandHandler(ITeamRepository repo, IRankingProjector rankingProjector)
    {
        _repo = repo;
        _rankingProjector = rankingProjector;
    }

    public async Task<Result<int>> Handle(AnswerTriviaCommand request, CancellationToken cancellationToken)
    {
        var team = await _repo.GetByIdAsync(request.TeamId, cancellationToken);
        if (team is null)
            return Result.Failure<int>(TeamErrors.NotFound);

        var result = team.AnswerTrivia(request.IsCorrect, request.ScoreChange, request.NextStageOrder);

        // HU-24: refresh the ranking projection (Score + LastStageCompletedAt changed).
        await _rankingProjector.RebuildAsync(team.SessionId, cancellationToken);

        await _repo.SaveChangesAsync(cancellationToken);
        return result;
    }
}
