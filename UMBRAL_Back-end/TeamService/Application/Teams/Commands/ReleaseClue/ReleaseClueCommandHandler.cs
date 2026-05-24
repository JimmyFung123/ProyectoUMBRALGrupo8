namespace TeamService.Application.Teams.Commands.ReleaseClue;

using MediatR;
using TeamService.Domain.Common;
using TeamService.Domain.Teams;

public class ReleaseClueCommandHandler : IRequestHandler<ReleaseClueCommand, Result<int>>
{
    private readonly ITeamRepository _teamRepository;

    public ReleaseClueCommandHandler(ITeamRepository teamRepository)
        => _teamRepository = teamRepository;

    public async Task<Result<int>> Handle(ReleaseClueCommand request, CancellationToken cancellationToken)
    {
        var team = await _teamRepository.GetByIdAsync(request.TeamId, cancellationToken);
        if (team is null)
            return Result.Failure<int>(TeamErrors.NotFound);

        var result = team.ReceiveClue(request.TotalCluesForStage, request.IsAutomatic);
        if (result.IsFailure)
            return result;

        await _teamRepository.SaveChangesAsync(cancellationToken);

        return result;
    }
}
