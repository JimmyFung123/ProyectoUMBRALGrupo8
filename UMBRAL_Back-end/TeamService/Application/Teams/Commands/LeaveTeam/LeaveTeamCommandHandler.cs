namespace TeamService.Application.Teams.Commands.LeaveTeam;

using MediatR;
using TeamService.Application.Rankings;
using TeamService.Domain.Common;
using TeamService.Domain.Teams;

public class LeaveTeamCommandHandler : IRequestHandler<LeaveTeamCommand, Result<bool>>
{
    private readonly ITeamRepository _repo;
    private readonly IRankingProjector _rankingProjector;

    public LeaveTeamCommandHandler(ITeamRepository repo, IRankingProjector rankingProjector)
    {
        _repo = repo;
        _rankingProjector = rankingProjector;
    }

    public async Task<Result<bool>> Handle(LeaveTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _repo.GetByIdAsync(request.TeamId, cancellationToken);
        if (team is null) return Result.Failure<bool>(TeamErrors.NotFound);

        var isEmpty = team.Leave();

        if (isEmpty)
        {
            await _repo.DeleteAsync(team.Id, cancellationToken);

            // HU-24: the team is gone — refresh the ranking projection so it no longer appears.
            await _rankingProjector.RebuildAsync(team.SessionId, cancellationToken);
        }

        await _repo.SaveChangesAsync(cancellationToken);

        return Result.Success(isEmpty);
    }
}
