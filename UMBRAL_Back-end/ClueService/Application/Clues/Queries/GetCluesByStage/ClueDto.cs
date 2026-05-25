namespace ClueService.Application.Clues.Queries.GetCluesByStage;
public record ClueDto(
    Guid Id,
    Guid StageId,
    Guid MissionId,
    string StageType,
    int Order,
    string? Content,
    double? Latitude,
    double? Longitude,
    int? RadiusMeters,
    int? AutoReleaseAfterMinutes,
    DateTime CreatedAt);
