namespace SessionService.Application.Sessions.Queries.GetReleasedClues;

using MediatR;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

/// <summary>
/// Returns the ordered list of clues that have already been released to a team
/// for its current stage. Used by participants to re-sync after a SignalR reconnect.
/// </summary>
public class GetReleasedCluesQueryHandler
    : IRequestHandler<GetReleasedCluesQuery, Result<ReleasedCluesDto>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamClient;
    private readonly IStageServiceClient _stageClient;
    private readonly IClueServiceClient _clueClient;

    public GetReleasedCluesQueryHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient,
        IStageServiceClient stageClient,
        IClueServiceClient clueClient)
    {
        _sessionRepository = sessionRepository;
        _teamClient = teamClient;
        _stageClient = stageClient;
        _clueClient = clueClient;
    }

    public async Task<Result<ReleasedCluesDto>> Handle(
        GetReleasedCluesQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<ReleasedCluesDto>(SessionErrors.NotFound);

        var teams = await _teamClient.GetTeamProgressAsync(request.SessionId, cancellationToken);
        var team = teams.FirstOrDefault(t => t.Id == request.TeamId);
        if (team is null)
            return Result.Failure<ReleasedCluesDto>(SessionErrors.TeamNotFound);

        // Team has not started or has already finished — no clues to return
        if (team.CurrentStageOrder <= 0)
            return Result.Success(new ReleasedCluesDto(Guid.Empty, 0, "Waiting", 0, 0, []));

        var stages = await _stageClient.GetStagesByMissionAsync(session.MissionId, cancellationToken);
        var stageRef = stages.FirstOrDefault(s => s.Order == team.CurrentStageOrder);
        if (stageRef is null)
            return Result.Success(new ReleasedCluesDto(Guid.Empty, team.CurrentStageOrder, "Completed", 0, 0, []));

        var stageDetails = await _stageClient.GetStageWithOptionsAsync(stageRef.Id, cancellationToken);
        var stageType = stageDetails?.Type ?? "Trivia";

        var allClues = await _clueClient.GetCluesByStageAsync(stageRef.Id, cancellationToken);
        var ordered = allClues.OrderBy(c => c.Order).ToList();

        var releasedCount = Math.Min(team.CluesReceivedCurrentStage, ordered.Count);
        var releasedClues = ordered
            .Take(releasedCount)
            .Select(c => new ReleasedClueDto(c.Id, c.Order, c.Content, c.Latitude, c.Longitude, c.RadiusMeters))
            .ToList();

        return Result.Success(new ReleasedCluesDto(
            stageRef.Id,
            team.CurrentStageOrder,
            stageType,
            releasedCount,
            ordered.Count,
            releasedClues));
    }
}
