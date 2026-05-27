namespace TeamService.Domain.Teams;

using TeamService.Domain.Common;

/// <summary>
/// A team enrolled in a session. Tracks connection status, stage progress, and score.
/// Owned by TeamService — SessionService is decoupled from this entity.
/// </summary>
public class Team
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    /// <summary>Short code the team leader shares so members can join (e.g. "XY42").</summary>
    public string InviteCode { get; private set; } = string.Empty;

    /// <summary>Number of participants currently in this team (leader counts as 1).</summary>
    public int MemberCount { get; private set; }

    /// <summary>Whether the team has an active WebSocket connection.</summary>
    public bool IsConnected { get; private set; }

    /// <summary>Sequential order of the stage the team is on (0 = not started).</summary>
    public int CurrentStageOrder { get; private set; }

    /// <summary>Clues received for the current stage.</summary>
    public int CluesReceivedCurrentStage { get; private set; }

    /// <summary>Total clues received across all stages.</summary>
    public int TotalCluesReceived { get; private set; }

    /// <summary>Accumulated score used for ranking.</summary>
    public int Score { get; private set; }

    /// <summary>When the team last entered a new stage or received a clue (timer start for auto-release).</summary>
    public DateTime? ClueTimerResetAt { get; private set; }

    /// <summary>Whether the last clue released to this team was automatic (system-triggered).</summary>
    public bool LastClueWasAutomatic { get; private set; }

    /// <summary>
    /// Timestamp of the team's last legitimately-resolved stage. Used as the tie-breaker
    /// for the live ranking (HU-21, RB-08): on equal score, the team that resolved its
    /// most recent stage earlier wins. Null until the team completes its first stage.
    /// Forced advances do NOT update this field — they don't count as resolution time.
    /// </summary>
    public DateTime? LastStageCompletedAt { get; private set; }

    /// <summary>
    /// When the team entered its <see cref="CurrentStageOrder"/>. Used by HU-25 to
    /// compute per-stage elapsed time for the historical analytics dashboard.
    /// Null while the team is in the waiting room (CurrentStageOrder = 0).
    /// </summary>
    public DateTime? CurrentStageStartedAt { get; private set; }

    private Team() { }

    public static Team Create(Guid sessionId, string name)
    {
        return new Team
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Name = name.Trim(),
            InviteCode = GenerateInviteCode(),
            MemberCount = 1,
            IsConnected = false,
            CurrentStageOrder = 0,
            CluesReceivedCurrentStage = 0,
            TotalCluesReceived = 0,
            Score = 0,
        };
    }

    /// <summary>Adds a member to this team. Succeeds up to MaxMembers (no hard limit enforced here).</summary>
    public Result<int> Join()
    {
        MemberCount++;
        return Result.Success(MemberCount);
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no ambiguous chars
        return new string(Enumerable.Range(0, 4)
            .Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
    }

    public void SetConnected(bool connected) => IsConnected = connected;

    public void UpdateProgress(int stageOrder, int cluesCurrentStage, int totalClues)
    {
        if (stageOrder != CurrentStageOrder)
        {
            var now = DateTime.UtcNow;
            ClueTimerResetAt = now;
            CurrentStageStartedAt = now;
            LastClueWasAutomatic = false;
            CluesReceivedCurrentStage = 0;
        }
        CurrentStageOrder = stageOrder;
        CluesReceivedCurrentStage = cluesCurrentStage;
        TotalCluesReceived = totalClues;
    }

    public void UpdateScore(int score) => Score = score;

    /// <summary>
    /// Subtracts points from the team's score as a penalty.
    /// Requires a non-empty reason. Points must be positive.
    /// Score can go negative (no floor enforced).
    /// </summary>
    public Result<int> Penalize(int points, string reason)
    {
        if (points <= 0)
            return Result.Failure<int>(TeamErrors.InvalidPenaltyPoints);
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure<int>(TeamErrors.PenaltyReasonRequired);

        Score -= points;
        return Result.Success(Score);
    }

    /// <summary>
    /// Forces the team to advance to the specified next stage.
    /// Score is NOT modified (team earns 0 points for the skipped stage).
    /// Resets clue tracking for the new stage.
    /// Returns the seconds the team spent on the stage being abandoned — used by
    /// HU-25 to record the analytics fact row. Zero if the team never started a stage.
    /// </summary>
    public Result<StageTransitionOutcome> ForceAdvance(int nextStageOrder)
    {
        if (nextStageOrder <= CurrentStageOrder)
            return Result.Failure<StageTransitionOutcome>(TeamErrors.InvalidNextStage);

        var now = DateTime.UtcNow;
        var elapsedSeconds = ComputeStageElapsedSeconds(now);

        CurrentStageOrder = nextStageOrder;
        CluesReceivedCurrentStage = 0;
        ClueTimerResetAt = now;
        CurrentStageStartedAt = now;
        LastClueWasAutomatic = false;
        return Result.Success(new StageTransitionOutcome(Score, elapsedSeconds));
    }

    /// <summary>
    /// Updates the team's score and advances to the next stage after answering a trivia question.
    /// Score increases on correct answer, decreases on incorrect. Resets clue tracking.
    /// Returns the seconds the team spent on the stage being abandoned — used by
    /// HU-25 to record the analytics fact row.
    /// </summary>
    public Result<StageTransitionOutcome> AnswerTrivia(bool isCorrect, int scoreChange, int nextStageOrder)
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = ComputeStageElapsedSeconds(now);

        Score += isCorrect ? scoreChange : -scoreChange;
        CurrentStageOrder = nextStageOrder;
        CluesReceivedCurrentStage = 0;
        ClueTimerResetAt = now;
        CurrentStageStartedAt = now;
        LastClueWasAutomatic = false;
        if (isCorrect)
            LastStageCompletedAt = now;
        return Result.Success(new StageTransitionOutcome(Score, elapsedSeconds));
    }

    /// <summary>
    /// Seconds the team spent on its current stage before transitioning. Returns 0
    /// when the team had no recorded stage start time (e.g. waiting room, or pre-HU-25
    /// rows whose nullable column was never populated).
    /// </summary>
    private int ComputeStageElapsedSeconds(DateTime now)
    {
        if (!CurrentStageStartedAt.HasValue) return 0;
        var diff = now - CurrentStageStartedAt.Value;
        return diff.TotalSeconds <= 0 ? 0 : (int)Math.Round(diff.TotalSeconds);
    }

    /// <summary>
    /// Records the release of the next sequential clue to this team for their current stage.
    /// Fails if all clues for the stage have already been released.
    /// </summary>
    /// <param name="totalCluesForStage">Total number of configured clues for the team's current stage.</param>
    /// <param name="isAutomatic">True when the clue is released automatically by the timer worker.</param>
    public Result<int> ReceiveClue(int totalCluesForStage, bool isAutomatic = false)
    {
        if (CluesReceivedCurrentStage >= totalCluesForStage)
            return Result.Failure<int>(TeamErrors.AllCluesReleased);

        CluesReceivedCurrentStage++;
        TotalCluesReceived++;
        ClueTimerResetAt = DateTime.UtcNow;
        LastClueWasAutomatic = isAutomatic;
        return Result.Success(CluesReceivedCurrentStage);
    }
}
