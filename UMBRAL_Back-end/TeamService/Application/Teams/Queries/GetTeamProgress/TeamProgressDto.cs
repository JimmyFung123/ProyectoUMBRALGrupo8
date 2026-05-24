namespace TeamService.Application.Teams.Queries.GetTeamProgress;

/// <summary>
/// Per-team snapshot for the operational dashboard ranking panel.
/// Teams are sorted by Score descending. Ties share the same rank (dense ranking).
/// </summary>
public record TeamProgressDto(
    Guid Id,
    string Name,
    bool IsConnected,
    int CurrentStageOrder,
    int Score,
    int Rank
);
