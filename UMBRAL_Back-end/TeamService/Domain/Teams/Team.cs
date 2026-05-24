namespace TeamService.Domain.Teams;

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
        CurrentStageOrder = stageOrder;
        CluesReceivedCurrentStage = cluesCurrentStage;
        TotalCluesReceived = totalClues;
    }

    public void UpdateScore(int score) => Score = score;
}
