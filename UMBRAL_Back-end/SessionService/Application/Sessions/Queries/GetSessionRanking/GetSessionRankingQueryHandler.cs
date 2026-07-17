namespace SessionService.Application.Sessions.Queries.GetSessionRanking;

using MediatR;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

/// <summary>
/// Returns the live ranking snapshot (HU-21).
///
/// Pure read path — delegates to <see cref="ITeamServiceClient.GetSessionRankingAsync"/>,
/// which hits TeamService's optimized projection (CQRS read model). The session is
/// re-fetched here only to expose its current status alongside the ranking so the
/// UI can show "Sesión en preparación → todos en 0 puntos" (flujo alterno de HU-21).
/// </summary>
public class GetSessionRankingQueryHandler
    : IRequestHandler<GetSessionRankingQuery, Result<SessionRankingResponse>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamClient;

    public GetSessionRankingQueryHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient)
    {
        _sessionRepository = sessionRepository;
        _teamClient = teamClient;
    }

    public async Task<Result<SessionRankingResponse>> Handle(
        GetSessionRankingQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<SessionRankingResponse>(SessionErrors.NotFound);

        var snapshot = await _teamClient.GetSessionRankingAsync(request.SessionId, cancellationToken);

        // When TeamService is unreachable we still return a coherent response with
        // an empty list — the client can show the "desconectado / sincronizando"
        // indicator and retain its last known data (flujo alterno HU-21).
        var teams = snapshot?.Teams ?? new List<SessionRankingTeamItem>();
        var generatedAt = snapshot?.GeneratedAt ?? DateTime.UtcNow;

        // Special case: while the session has not yet started, all teams must be
        // shown with zero points and no resolution time (flujo alterno HU-21).
        if (session.Status == SessionStatus.Pending)
        {
            teams = teams
                .Select(t => t with { Score = 0, LastStageCompletedAt = null, Rank = 1 })
                .ToList();
        }

        var dto = new SessionRankingResponse(
            SessionId: session.Id,
            SessionStatus: session.Status.ToString(),
            GeneratedAt: generatedAt,
            Teams: teams
                .Select(t => new SessionRankingTeamDto(
                    t.TeamId,
                    t.Name,
                    t.Score,
                    t.Rank,
                    t.CurrentStageOrder,
                    t.IsConnected,
                    t.LastStageCompletedAt))
                .ToList());

        return Result.Success(dto);
    }
}
