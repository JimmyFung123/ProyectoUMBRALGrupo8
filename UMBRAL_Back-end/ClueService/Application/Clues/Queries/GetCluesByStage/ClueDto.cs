namespace ClueService.Application.Clues.Queries.GetCluesByStage;
public record ClueDto(Guid Id, Guid StageId, Guid MissionId, string Content, int Order, int? AutoReleaseAfterMinutes, DateTime CreatedAt);
