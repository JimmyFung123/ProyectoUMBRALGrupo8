namespace TeamService.Application.Teams.Commands.ForceAdvance;

using MediatR;
using TeamService.Domain.Common;
using TeamService.Domain.Teams;

public class ForceAdvanceTeamCommandHandler : IRequestHandler<ForceAdvanceTeamCommand, Result<bool>>
{
    private readonly ITeamRepository _repo;
    public ForceAdvanceTeamCommandHandler(ITeamRepository repo) => _repo = repo;

    public async Task<Result<bool>> Handle(ForceAdvanceTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _repo.GetByIdAsync(request.TeamId, cancellationToken);
        if (team is null) return Result.Failure<bool>(TeamErrors.NotFound);

        var result = team.ForceAdvance(request.NextStageOrder);
        if (result.IsFailure) return result;

        await _repo.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
