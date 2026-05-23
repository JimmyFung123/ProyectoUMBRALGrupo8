namespace ClueService.Domain.Clues;
using ClueService.Domain.Common;
public class Clue
{
    public Guid Id { get; private set; }
    public Guid StageId { get; private set; }
    public Guid MissionId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Clue() { }

    public static Result<Clue> Create(Guid stageId, Guid missionId, string content, int order)
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
            CreatedAt = DateTime.UtcNow
        });
    }

    public void UpdateContent(string content) => Content = content;
}
