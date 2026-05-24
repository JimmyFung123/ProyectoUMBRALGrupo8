namespace ClueService.Domain.Clues;
using ClueService.Domain.Common;
public class Clue
{
    public Guid Id { get; private set; }
    public Guid StageId { get; private set; }
    public Guid MissionId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public int? AutoReleaseAfterMinutes { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Clue() { }

    public static Result<Clue> Create(Guid stageId, Guid missionId, string content, int order, int? autoReleaseAfterMinutes = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Result.Failure<Clue>(ClueErrors.InvalidContent);

        return Result.Success(new Clue
        {
            Id = Guid.NewGuid(),
            StageId = stageId,
            MissionId = missionId,
            Content = content,
            Order = order,
            AutoReleaseAfterMinutes = autoReleaseAfterMinutes,
            CreatedAt = DateTime.UtcNow
        });
    }

    public void UpdateContent(string content) => Content = content;

    public void SetAutoRelease(int? minutes) => AutoReleaseAfterMinutes = minutes;
}
