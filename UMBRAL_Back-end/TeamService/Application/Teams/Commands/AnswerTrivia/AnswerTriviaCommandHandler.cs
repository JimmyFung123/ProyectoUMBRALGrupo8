namespace TeamService.Application.Teams.Commands.AnswerTrivia;

using MediatR;
using TeamService.Domain.Common;
using TeamService.Domain.Teams;

public class AnswerTriviaCommandHandler : IRequestHandler<AnswerTriviaCommand, Result<int>>
{
    private readonly ITeamRepository _repo;

    public AnswerTriviaCommandHandler(ITeamRepository repo) => _repo = repo;

    public async Task<Result<int>> Handle(AnswerTriviaCommand request, CancellationToken cancellationToken)
    {
        var team = await _repo.GetByIdAsync(request.TeamId, cancellationToken);
        if (team is null)
            return Result.Failure<int>(TeamErrors.NotFound);

        var result = team.AnswerTrivia(request.IsCorrect, request.ScoreChange, request.NextStageOrder);

        await _repo.SaveChangesAsync(cancellationToken);
        return result;
    }
}
