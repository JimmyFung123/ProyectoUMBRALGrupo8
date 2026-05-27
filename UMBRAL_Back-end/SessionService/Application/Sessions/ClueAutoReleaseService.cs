namespace SessionService.Application.Sessions;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SessionService.Domain.Sessions;
using SessionService.Infrastructure.Hubs;

/// <summary>
/// Processes automatic clue release for all InProgress sessions.
/// Extracted from the BackgroundService for testability.
/// </summary>
public class ClueAutoReleaseService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamClient;
    private readonly IStageServiceClient _stageClient;
    private readonly IClueServiceClient _clueClient;
    private readonly ISessionEventRepository _eventRepository;
    private readonly IHubContext<SessionHub> _hub;
    private readonly ILogger<ClueAutoReleaseService> _logger;

    public ClueAutoReleaseService(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient,
        IStageServiceClient stageClient,
        IClueServiceClient clueClient,
        ISessionEventRepository eventRepository,
        IHubContext<SessionHub> hub,
        ILogger<ClueAutoReleaseService> logger)
    {
        _sessionRepository = sessionRepository;
        _teamClient = teamClient;
        _stageClient = stageClient;
        _clueClient = clueClient;
        _eventRepository = eventRepository;
        _hub = hub;
        _logger = logger;
    }

    public async Task ProcessAsync(CancellationToken ct)
    {
        var sessions = await _sessionRepository.GetAllInProgressAsync(ct);

        foreach (var session in sessions)
        {
            try { await ProcessSessionAsync(session, ct); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto-release failed for session {SessionId}", session.Id);
            }
        }
    }

    private async Task ProcessSessionAsync(Session session, CancellationToken ct)
    {
        var teams = await _teamClient.GetTeamProgressAsync(session.Id, ct);
        var eligibleTeams = teams.Where(t =>
            t.CurrentStageOrder > 0 &&
            t.ClueTimerResetAt.HasValue).ToList();

        if (eligibleTeams.Count == 0) return;

        var stages = await _stageClient.GetStagesByMissionAsync(session.MissionId, ct);

        foreach (var team in eligibleTeams)
        {
            var stage = stages.FirstOrDefault(s => s.Order == team.CurrentStageOrder);
            if (stage is null) continue;

            var clues = await _clueClient.GetCluesByStageAsync(stage.Id, ct);
            if (clues.Count == 0) continue;

            // Next clue to release (0-based index)
            var nextClue = clues.ElementAtOrDefault(team.CluesReceivedCurrentStage);
            if (nextClue is null) continue; // all clues already released
            if (nextClue.AutoReleaseAfterMinutes is null) continue; // no auto-release configured

            var elapsed = DateTime.UtcNow - team.ClueTimerResetAt!.Value;
            if (elapsed.TotalMinutes < nextClue.AutoReleaseAfterMinutes.Value) continue;

            // Release automatically
            var clueNumber = await _teamClient.ReleaseClueAsync(
                team.Id, clues.Count, ct, isAutomatic: true);

            if (clueNumber <= 0) continue; // exhausted or error

            _logger.LogInformation(
                "Auto-released clue {ClueNumber}/{Total} to team {TeamId} in session {SessionId}",
                clueNumber, clues.Count, team.Id, session.Id);

            // HU-14 criterion 2 + HU-22 / HU-26: record on the audit timeline as "Sistema".
            // SessionEvent.Create defaults actorName to "Sistema" when omitted.
            var auditMessage = nextClue.Content is not null
                ? $"Pista #{clueNumber} liberada automáticamente al equipo '{team.Name}': \"{nextClue.Content}\"."
                : $"Pista #{clueNumber} liberada automáticamente al equipo '{team.Name}': zona geográfica (radio {nextClue.RadiusMeters ?? 0}m).";
            await _eventRepository.AddAsync(
                SessionEvent.Create(
                    session.Id,
                    auditMessage,
                    commandType: "AutoReleaseClue",
                    outcome: SessionEvent.OutcomeSuccess),
                ct);
            await _eventRepository.SaveChangesAsync(ct);

            await _hub.Clients.Group(session.Id.ToString())
                .SendAsync("ClueReleased", new
                {
                    sessionId        = session.Id,
                    teamId           = team.Id,
                    clueContent      = nextClue.Content,
                    clueLatitude     = nextClue.Latitude,
                    clueLongitude    = nextClue.Longitude,
                    clueRadiusMeters = nextClue.RadiusMeters,
                    clueNumber,
                    isAutomatic      = true,
                }, ct);
        }
    }
}
