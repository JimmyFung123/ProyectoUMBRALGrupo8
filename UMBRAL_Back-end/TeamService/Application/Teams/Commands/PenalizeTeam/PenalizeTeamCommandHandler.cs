namespace TeamService.Application.Teams.Commands.PenalizeTeam;

using MediatR;
using TeamService.Domain.Common;
using TeamService.Domain.Teams;

public class PenalizeTeamCommandHandler : IRequestHandler<PenalizeTeamCommand, Result<int>>
{
    private readonly ITeamRepository _repo;
    public PenalizeTeamCommandHandler(ITeamRepository repo) => _repo = repo;

    public async Task<Result<int>> Handle(PenalizeTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _repo.GetByIdAsync(request.TeamId, cancellationToken);
        if (team is null) return Result.Failure<int>(TeamErrors.NotFound);

        var result = team.Penalize(request.Points, request.Reason);
        if (result.IsFailure) return result;

        await _repo.SaveChangesAsync(cancellationToken);
        return result;
    }
}
