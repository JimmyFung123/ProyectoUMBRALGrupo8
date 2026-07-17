namespace TeamService.Application.Teams.Queries.GetTeamById;

using MediatR;
using TeamService.Domain.Common;
using TeamService.Domain.Teams;

public class GetTeamByIdQueryHandler : IRequestHandler<GetTeamByIdQuery, Result<TeamInfoDto>>
{
    private readonly ITeamRepository _teamRepository;

    public GetTeamByIdQueryHandler(ITeamRepository teamRepository)
        => _teamRepository = teamRepository;

    public async Task<Result<TeamInfoDto>> Handle(GetTeamByIdQuery request, CancellationToken cancellationToken)
    {
        var team = await _teamRepository.GetByIdAsync(request.TeamId, cancellationToken);
        if (team is null)
            return Result.Failure<TeamInfoDto>(TeamErrors.NotFound);

        return Result.Success(new TeamInfoDto(
            team.Id,
            team.Name,
            team.InviteCode,
            team.MemberCount,
            team.CurrentStageOrder));
    }
}
