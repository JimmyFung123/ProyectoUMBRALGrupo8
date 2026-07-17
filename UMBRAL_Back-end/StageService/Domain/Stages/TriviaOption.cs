namespace StageService.Domain.Stages;

public class TriviaOption
{
    public Guid Id { get; private set; }
    public Guid StageId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public bool IsCorrect { get; private set; }

    private TriviaOption() { }

    public TriviaOption(Guid id, Guid stageId, string text, bool isCorrect)
    {
        Id = id;
        StageId = stageId;
        Text = text;
        IsCorrect = isCorrect;
    }
}
