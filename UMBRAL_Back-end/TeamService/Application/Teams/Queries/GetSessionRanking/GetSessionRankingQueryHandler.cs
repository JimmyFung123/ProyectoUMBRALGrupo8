namespace TeamService.Application.Teams.Queries.GetSessionRanking;

using MediatR;
using TeamService.Domain.Rankings;

/// <summary>
/// Reads the live ranking for a session (HU-24).
///
/// This is a pure CQRS read path: the handler hits the pre-calculated
/// <c>RankingProjections</c> table — which is already sorted by
/// <c>Position</c> and already carries the computed <c>Rank</c> — and maps
/// the rows 1-to-1 onto <see cref="SessionRankingDto"/>.
///
/// There is no JOIN, no sorting in C#, no rank arithmetic at SELECT time.
/// The transactional <c>Teams</c> table is never touched on the read path:
/// score updates, penalties, forced advances etc. cannot block this query
/// even under heavy concurrent writes (the original reason CQRS was chosen).
///
/// The projection is maintained synchronously by <c>IRankingProjector</c>
/// inside the write-side command handlers — see <c>RankingProjector</c>.
/// </summary>
public class GetSessionRankingQueryHandler
    : IRequestHandler<GetSessionRankingQuery, SessionRankingDto>
{
    private readonly IRankingProjectionRepository _rankingRepository;

    public GetSessionRankingQueryHandler(IRankingProjectionRepository rankingRepository)
        => _rankingRepository = rankingRepository;

    public async Task<SessionRankingDto> Handle(
        GetSessionRankingQuery request,
        CancellationToken cancellationToken)
    {
        var rows = await _rankingRepository.GetBySessionIdOrderedAsync(
            request.SessionId, cancellationToken);

        var teams = rows
            .Select(r => new SessionRankingTeamDto(
                TeamId: r.TeamId,
                Name: r.TeamName,
                Score: r.Score,
                Rank: r.Rank,
                CurrentStageOrder: r.CurrentStageOrder,
                IsConnected: r.IsConnected,
                LastStageCompletedAt: r.LastStageCompletedAt))
            .ToList();

        return new SessionRankingDto(
            SessionId: request.SessionId,
            GeneratedAt: DateTime.UtcNow,
            Teams: teams);
    }
}
