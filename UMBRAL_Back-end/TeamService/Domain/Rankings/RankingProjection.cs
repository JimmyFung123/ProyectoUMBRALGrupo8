namespace TeamService.Domain.Rankings;

/// <summary>
/// CQRS read-model row for the live ranking (HU-24).
///
/// This is a pre-calculated, denormalized projection — completely separate from
/// the transactional <c>Teams</c> table. Reads of the ranking only touch this
/// table and do not run any sorting or rank arithmetic at SELECT time: the
/// rows are already stored in the desired order via <see cref="Position"/>.
///
/// The projection is rebuilt (per session) by <c>IRankingProjector</c> after
/// every write that affects the leaderboard.
/// </summary>
public class RankingProjection
{
    /// <summary>Session this leaderboard row belongs to. Part of the composite key.</summary>
    public Guid SessionId { get; private set; }

    /// <summary>Team mirrored by this row. Part of the composite key.</summary>
    public Guid TeamId { get; private set; }

    /// <summary>Denormalized team name (avoids any JOIN on read).</summary>
    public string TeamName { get; private set; } = string.Empty;

    /// <summary>Accumulated score copied from the write model at projection time.</summary>
    public int Score { get; private set; }

    /// <summary>
    /// Dense rank inside the session (1-based). Tied teams share the same
    /// <see cref="Rank"/>; the next distinct score gets the next integer.
    /// Stored — never computed on read.
    /// </summary>
    public int Rank { get; private set; }

    /// <summary>
    /// Absolute 1-based row order inside the session. The query handler reads
    /// <c>ORDER BY Position</c> and returns the rows as-is. Two tied teams have
    /// different <see cref="Position"/> values but the same <see cref="Rank"/>.
    /// </summary>
    public int Position { get; private set; }

    public int CurrentStageOrder { get; private set; }
    public bool IsConnected { get; private set; }

    /// <summary>Time of the team's last legitimately-resolved stage (RB-08 tie-breaker).</summary>
    public DateTime? LastStageCompletedAt { get; private set; }

    /// <summary>When this row was last refreshed by the projector.</summary>
    public DateTime UpdatedAt { get; private set; }

    private RankingProjection() { }

    public static RankingProjection Create(
        Guid sessionId,
        Guid teamId,
        string teamName,
        int score,
        int rank,
        int position,
        int currentStageOrder,
        bool isConnected,
        DateTime? lastStageCompletedAt,
        DateTime updatedAt)
    {
        return new RankingProjection
        {
            SessionId = sessionId,
            TeamId = teamId,
            TeamName = teamName,
            Score = score,
            Rank = rank,
            Position = position,
            CurrentStageOrder = currentStageOrder,
            IsConnected = isConnected,
            LastStageCompletedAt = lastStageCompletedAt,
            UpdatedAt = updatedAt,
        };
    }

    /// <summary>
    /// Updates this row in place so the projector can rebuild a session
    /// without deleting and re-inserting every row on every write.
    /// </summary>
    public void Refresh(
        string teamName,
        int score,
        int rank,
        int position,
        int currentStageOrder,
        bool isConnected,
        DateTime? lastStageCompletedAt,
        DateTime updatedAt)
    {
        TeamName = teamName;
        Score = score;
        Rank = rank;
        Position = position;
        CurrentStageOrder = currentStageOrder;
        IsConnected = isConnected;
        LastStageCompletedAt = lastStageCompletedAt;
        UpdatedAt = updatedAt;
    }
}
