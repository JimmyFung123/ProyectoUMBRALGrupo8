namespace TeamService.Application.Teams.Queries.GetTeamProgress;

using MediatR;
using TeamService.Domain.Teams;

public class GetTeamProgressQueryHandler
    : IRequestHandler<GetTeamProgressQuery, IReadOnlyList<TeamProgressDto>>
{
    private readonly ITeamRepository _teamRepository;

    public GetTeamProgressQueryHandler(ITeamRepository teamRepository)
        => _teamRepository = teamRepository;

    public async Task<IReadOnlyList<TeamProgressDto>> Handle(
        GetTeamProgressQuery request,
        CancellationToken cancellationToken)
    {
        var teams = await _teamRepository.GetBySessionIdAsync(request.SessionId, cancellationToken);

        var sorted = teams
            .OrderByDescending(t => t.Score)
            .ThenBy(t => t.Name)
            .ToList();

        int rank = 1;
        var result = new List<TeamProgressDto>(sorted.Count);

        for (int i = 0; i < sorted.Count; i++)
        {
            if (i > 0 && sorted[i].Score < sorted[i - 1].Score)
                rank = i + 1;

            result.Add(new TeamProgressDto(
                Id: sorted[i].Id,
                Name: sorted[i].Name,
                IsConnected: sorted[i].IsConnected,
                CurrentStageOrder: sorted[i].CurrentStageOrder,
                CluesReceivedCurrentStage: sorted[i].CluesReceivedCurrentStage,
                Score: sorted[i].Score,
                Rank: rank,
                ClueTimerResetAt: sorted[i].ClueTimerResetAt,
                LastClueWasAutomatic: sorted[i].LastClueWasAutomatic));
        }

        return result;
    }
}
