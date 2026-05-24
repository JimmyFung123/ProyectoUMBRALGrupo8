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

    private Team() { }

    public static Team Create(Guid sessionId, string name)
    {
        return new Team
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Name = name.Trim(),
            IsConnected = false,
            CurrentStageOrder = 0,
            CluesReceivedCurrentStage = 0,
            TotalCluesReceived = 0,
            Score = 0,
        };
    }

    public void SetConnected(bool connected) => IsConnected = connected;

    public void UpdateProgress(int stageOrder, int cluesCurrentStage, int totalClues)
    {
        if (stageOrder != CurrentStageOrder)
        {
            ClueTimerResetAt = DateTime.UtcNow;
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
