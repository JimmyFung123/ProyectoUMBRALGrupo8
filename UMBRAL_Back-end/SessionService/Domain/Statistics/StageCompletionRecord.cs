namespace SessionService.Domain.Statistics;

/// <summary>
/// Immutable fact-table row for the historical analytics dashboard (HU-25).
///
/// One record is inserted every time a team transitions out of a stage —
/// whether by trivia answer, QR validation or forced advance. The dashboard
/// query never JOINs against the live <c>Sessions</c> / <c>Teams</c> tables:
/// it reads this fact table directly, filtered by <see cref="IncludedInStatistics"/>,
/// so analytics queries cannot lock or slow down live gameplay writes
/// (criterion 2 of HU-25).
///
/// The <see cref="IncludedInStatistics"/> flag is flipped to <c>true</c> by
/// <c>FinalizeSessionCommandHandler</c> when a session reaches the
/// <c>Finalized</c> state. Records from active / paused / cancelled sessions
/// remain invisible to the dashboard until the session is properly closed
/// (criterion 1 of HU-25 — "métricas históricas de sesiones finalizadas").
/// </summary>
public class StageCompletionRecord
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }

    /// <summary>Denormalized mission id so the dashboard can filter without JOINs.</summary>
    public Guid MissionId { get; private set; }

    public Guid TeamId { get; private set; }

    /// <summary>1-based order of the stage that was just completed (the one the team left).</summary>
    public int StageOrder { get; private set; }

    /// <summary>"Trivia" or "TreasureHunt" — denormalized from the stage.</summary>
    public string StageType { get; private set; } = string.Empty;

    /// <summary>Seconds the team spent on the stage. Computed by TeamService at transition time.</summary>
    public int ElapsedSeconds { get; private set; }

    /// <summary>
    /// True/false for trivia and QR validation, null for forced advances
    /// (the team didn't really answer — the operator overrode the flow).
    /// </summary>
    public bool? WasCorrect { get; private set; }

    /// <summary>True when the row comes from a force-advance operation.</summary>
    public bool WasForceAdvance { get; private set; }

    /// <summary>Wall-clock time when the transition was recorded.</summary>
    public DateTime RecordedAt { get; private set; }

    /// <summary>
    /// Visibility flag for the dashboard. Set to <c>true</c> when the parent
    /// session is finalized; rows with <c>false</c> are invisible to analytics
    /// queries so in-flight games never bleed into the historical numbers.
    /// </summary>
    public bool IncludedInStatistics { get; private set; }

    private StageCompletionRecord() { }

    public static StageCompletionRecord ForTriviaAnswer(
        Guid sessionId,
        Guid missionId,
        Guid teamId,
        int stageOrder,
        int elapsedSeconds,
        bool wasCorrect) =>
        new()
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            MissionId = missionId,
            TeamId = teamId,
            StageOrder = stageOrder,
            StageType = "Trivia",
            ElapsedSeconds = elapsedSeconds,
            WasCorrect = wasCorrect,
            WasForceAdvance = false,
            RecordedAt = DateTime.UtcNow,
            IncludedInStatistics = false,
        };

    public static StageCompletionRecord ForTreasureHuntScan(
        Guid sessionId,
        Guid missionId,
        Guid teamId,
        int stageOrder,
        int elapsedSeconds,
        bool wasCorrect) =>
        new()
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            MissionId = missionId,
            TeamId = teamId,
            StageOrder = stageOrder,
            StageType = "TreasureHunt",
            ElapsedSeconds = elapsedSeconds,
            WasCorrect = wasCorrect,
            WasForceAdvance = false,
            RecordedAt = DateTime.UtcNow,
            IncludedInStatistics = false,
        };

    public static StageCompletionRecord ForForceAdvance(
        Guid sessionId,
        Guid missionId,
        Guid teamId,
        int stageOrder,
        string stageType,
        int elapsedSeconds) =>
        new()
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            MissionId = missionId,
            TeamId = teamId,
            StageOrder = stageOrder,
            StageType = stageType,
            ElapsedSeconds = elapsedSeconds,
            WasCorrect = null, // not an answer — operator override
            WasForceAdvance = true,
            RecordedAt = DateTime.UtcNow,
            IncludedInStatistics = false,
        };

    /// <summary>
    /// Marks the record as visible to the analytics dashboard. Called by the
    /// FinalizeSession handler in bulk once the parent session is closed.
    /// </summary>
    public void IncludeInStatistics() => IncludedInStatistics = true;
}
