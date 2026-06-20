namespace SessionService.Application.Sessions;

using Microsoft.Extensions.Logging;
using SessionService.Application;
using SessionService.Domain.Sessions;
using UMBRAL.Contracts.Events;

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
    private readonly IIntegrationEventBus _bus;
    private readonly ISessionNotifier _notifier;
    private readonly ILogger<ClueAutoReleaseService> _logger;

    public ClueAutoReleaseService(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient,
        IStageServiceClient stageClient,
        IClueServiceClient clueClient,
        IIntegrationEventBus bus,
        ISessionNotifier notifier,
        ILogger<ClueAutoReleaseService> logger)
    {
        _sessionRepository = sessionRepository;
        _teamClient = teamClient;
        _stageClient = stageClient;
        _clueClient = clueClient;
        _bus = bus;
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
            _logger.LogDebug("Session {SessionId}: no eligible teams for auto-release", session.Id);
            return;
        }

        var stages = await _stageClient.GetStagesByMissionAsync(session.MissionId, ct);

        foreach (var team in eligibleTeams)
        {
            var stage = stages.FirstOrDefault(s => s.Order == team.CurrentStageOrder);
            if (stage is null)
            {
                _logger.LogDebug("Session {SessionId} team {TeamId}: stage order {Order} not found", session.Id, team.Id, team.CurrentStageOrder);
                continue;
            }

            var threshold = stage.AutoReleaseTimeMinutes;
            if (threshold is null)
            {
                _logger.LogDebug("Session {SessionId} team {TeamId}: stage {StageId} has no AutoReleaseTimeMinutes", session.Id, team.Id, stage.Id);
                continue;
            }

            var clues = await _clueClient.GetCluesByStageAsync(stage.Id, ct);
            if (clues.Count == 0)
            {
                _logger.LogDebug("Session {SessionId} team {TeamId}: stage {StageId} has no clues", session.Id, team.Id, stage.Id);
                continue;
            }

            var nextClue = clues.ElementAtOrDefault(team.CluesReceivedCurrentStage);
            if (nextClue is null)
            {
                _logger.LogDebug("Session {SessionId} team {TeamId}: all {Count} clues already released", session.Id, team.Id, clues.Count);
                continue;
            }

            var elapsed = DateTime.UtcNow - team.ClueTimerResetAt!.Value;
            if (elapsed.TotalMinutes < threshold.Value)
            {
                _logger.LogDebug("Session {SessionId} team {TeamId}: elapsed {Elapsed:F1}min < threshold {Threshold}min", session.Id, team.Id, elapsed.TotalMinutes, threshold.Value);
                continue;
            }

            var clueNumber = await _teamClient.ReleaseClueAsync(
                team.Id, clues.Count, ct, isAutomatic: true);

            if (clueNumber <= 0)
            {
                _logger.LogDebug("Session {SessionId} team {TeamId}: ReleaseClueAsync returned {ClueNumber}", session.Id, team.Id, clueNumber);
                continue;
            }

            _logger.LogInformation(
                "Auto-released clue {ClueNumber}/{Total} to team {TeamId} in session {SessionId}",
                clueNumber, clues.Count, team.Id, session.Id);

            // HU-14 criterion 2 + HU-22 / HU-26: record on the audit timeline as "Sistema".
            // SessionEvent.Create defaults actorName to "Sistema" when omitted, so the
            // integration event carries ActorName: null to preserve that same default.
            var auditMessage = nextClue.Content is not null
                ? $"Pista #{clueNumber} liberada automáticamente al equipo '{team.Name}': \"{nextClue.Content}\"."
                : $"Pista #{clueNumber} liberada automáticamente al equipo '{team.Name}': zona geográfica (radio {nextClue.RadiusMeters ?? 0}m).";
            await _bus.PublishAsync(
                new SessionAuditIntegrationEvent(
                    session.Id,
                    auditMessage,
                    ActorName: null,
                    CommandType: "AutoReleaseClue",
                    Outcome: SessionEvent.OutcomeSuccess,
                    DateTime.UtcNow),
                ct);

            await _notifier.NotifyClueReleasedAsync(
                session.Id, team.Id,
                nextClue.Content, nextClue.Latitude, nextClue.Longitude, nextClue.RadiusMeters,
                clueNumber, isAutomatic: true,
                ct);
        }
    }
}
