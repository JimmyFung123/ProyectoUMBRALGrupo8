namespace SessionService.Application.Missions.Queries.GetMissionStructure;

/// <summary>Serializable projection of the mission Composite tree + its summary.</summary>
public record MissionStructureDto(
    Guid MissionId,
    string Name,
    string Difficulty,
    string Status,
    MissionStructureSummaryDto Summary,
    IReadOnlyList<StageNodeDto> Stages);

public record StageNodeDto(
    Guid Id,
    string Title,
    string Type,
    int Order,
    int BaseScore,
    IReadOnlyList<ClueNodeDto> Clues);

public record ClueNodeDto(Guid Id, int Order, string? Content);

public record MissionStructureSummaryDto(
    int StageCount,
    int ClueCount,
    int TriviaStages,
    int TreasureHuntStages,
    int TotalScore,
    int StagesWithoutClues);
