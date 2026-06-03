namespace SessionService.Application.Sessions;

using Microsoft.Extensions.Logging;
using SessionService.Domain.Sessions;

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
    private readonly ISessionNotifier _notifier;
    private readonly ILogger<ClueAutoReleaseService> _logger;

    public ClueAutoReleaseService(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient,
        IStageServiceClient stageClient,
        IClueServiceClient clueClient,
        ISessionEventRepository eventRepository,
        ISessionNotifier notifier,
        ILogger<ClueAutoReleaseService> logger)
    {
        _sessionRepository = sessionRepository;
        _teamClient = teamClient;
        _stageClient = stageClient;
        _clueClient = clueClient;
        _eventRepository = eventRepository;
        _notifier = notifier;
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

        if (eligibleTeams.Count == 0)
        {
            _logger.LogDebug(
                "Session {SessionId}: no eligible teams (all at stage 0 or without ClueTimerResetAt). Total teams: {Total}",
                session.Id, teams.Count);
            return;
        }

        var stages = await _stageClient.GetStagesByMissionAsync(session.MissionId, ct);
        if (stages.Count == 0)
        {
            _logger.LogWarning(
                "Session {SessionId}: GetStagesByMissionAsync returned 0 stages for mission {MissionId}. " +
                "StageService may be unreachable or the mission has no stages.",
                session.Id, session.MissionId);
            return;
        }

        foreach (var team in eligibleTeams)
        {
            var stage = stages.FirstOrDefault(s => s.Order == team.CurrentStageOrder);
            if (stage is null)
            {
                _logger.LogWarning(
                    "Session {SessionId} / Team {TeamId}: no stage with Order={Order} found in mission {MissionId}. " +
                    "Available orders: {Orders}",
                    session.Id, team.Id, team.CurrentStageOrder, session.MissionId,
                    string.Join(", ", stages.Select(s => s.Order)));
                continue;
            }

            if (stage.AutoReleaseTimeMinutes is null)
            {
                _logger.LogDebug(
                    "Session {SessionId} / Team {TeamId}: stage {StageId} has no AutoReleaseTimeMinutes configured.",
                    session.Id, team.Id, stage.Id);
                continue;
            }

            var elapsed = DateTime.UtcNow - team.ClueTimerResetAt!.Value;
            if (elapsed.TotalMinutes < stage.AutoReleaseTimeMinutes.Value)
            {
                _logger.LogDebug(
                    "Session {SessionId} / Team {TeamId}: timer not expired — elapsed {Elapsed:F1} min / threshold {Threshold} min.",
                    session.Id, team.Id, elapsed.TotalMinutes, stage.AutoReleaseTimeMinutes.Value);
                continue;
            }

            var clues = await _clueClient.GetCluesByStageAsync(stage.Id, ct);
            if (clues.Count == 0)
            {
                _logger.LogDebug(
                    "Session {SessionId} / Team {TeamId}: stage {StageId} has no clues configured.",
                    session.Id, team.Id, stage.Id);
                continue;
            }

            // Next clue to release (0-based index)
            var nextClue = clues.ElementAtOrDefault(team.CluesReceivedCurrentStage);
            if (nextClue is null)
            {
                _logger.LogDebug(
                    "Session {SessionId} / Team {TeamId}: all {Count} clues already released.",
                    session.Id, team.Id, clues.Count);
                continue;
            }

            // Release automatically
            var clueNumber = await _teamClient.ReleaseClueAsync(
                team.Id, clues.Count, ct, isAutomatic: true);

            if (clueNumber <= 0)
            {
                _logger.LogWarning(
                    "Session {SessionId} / Team {TeamId}: ReleaseClueAsync returned {Result} (all clues exhausted or HTTP error).",
                    session.Id, team.Id, clueNumber);
                continue;
            }

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

            await _notifier.NotifyClueReleasedAsync(
                session.Id, team.Id,
                nextClue.Content, nextClue.Latitude, nextClue.Longitude, nextClue.RadiusMeters,
                clueNumber, isAutomatic: true,
                ct);
        }
    }
}
