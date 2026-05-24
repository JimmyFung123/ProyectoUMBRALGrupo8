namespace SessionService.Application.Sessions;

public interface IClueServiceClient
{
    Task<IReadOnlyList<ClueInfo>> GetCluesByStageAsync(Guid stageId, CancellationToken ct);
}

public record ClueInfo(Guid Id, string Content, int Order, int? AutoReleaseAfterMinutes);
