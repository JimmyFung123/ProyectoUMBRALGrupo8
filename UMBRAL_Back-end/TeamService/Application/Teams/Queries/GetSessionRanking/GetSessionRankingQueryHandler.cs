namespace TeamService.Application.Teams.Queries.GetSessionRanking;

using MediatR;
using TeamService.Domain.Teams;

/// <summary>
/// Builds the live ranking for a session (HU-21).
///
/// Sort contract (RB-08):
///   1. Higher score first.
///   2. On equal score, the team that completed its most recent stage earlier wins
///      (lower <see cref="Team.LastStageCompletedAt"/>). Teams that haven't completed
///      any stage yet are pushed to the bottom of the tie group.
///   3. Final stable tie-breaker: alphabetical by team name.
///
/// Rank uses dense ranking — tied teams share the same Rank, and the next distinct
/// score gets Rank = i + 1.
/// </summary>
public class GetSessionRankingQueryHandler
    : IRequestHandler<GetSessionRankingQuery, SessionRankingDto>
{
    private readonly ITeamRepository _teamRepository;

    public GetSessionRankingQueryHandler(ITeamRepository teamRepository)
        => _teamRepository = teamRepository;

    public async Task<SessionRankingDto> Handle(
        GetSessionRankingQuery request,
        CancellationToken cancellationToken)
    {
        var teams = await _teamRepository.GetBySessionIdAsync(request.SessionId, cancellationToken);

        // Teams without a recorded resolution time should lose tie-breaks against teams
        // that already completed a stage — push them to the end of the tie window.
        var sorted = teams
            .OrderByDescending(t => t.Score)
            .ThenBy(t => t.LastStageCompletedAt ?? DateTime.MaxValue)
            .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var ranked = new List<SessionRankingTeamDto>(sorted.Count);
        int rank = 1;

        for (int i = 0; i < sorted.Count; i++)
        {
            if (i > 0 && sorted[i].Score < sorted[i - 1].Score)
                rank = i + 1;

            ranked.Add(new SessionRankingTeamDto(
                TeamId: sorted[i].Id,
                Name: sorted[i].Name,
                Score: sorted[i].Score,
                Rank: rank,
                CurrentStageOrder: sorted[i].CurrentStageOrder,
                IsConnected: sorted[i].IsConnected,
                LastStageCompletedAt: sorted[i].LastStageCompletedAt));
        }

        return new SessionRankingDto(
            SessionId: request.SessionId,
            GeneratedAt: DateTime.UtcNow,
            Teams: ranked);
    }
}
