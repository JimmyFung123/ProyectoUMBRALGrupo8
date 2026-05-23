namespace UMBRAL_Back_end.Domain.Missions;

public class TriviaOption
{
    public Guid Id { get; private set; }
    public Guid StageId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public bool IsCorrect { get; private set; }

    private TriviaOption() { }

    internal static TriviaOption Create(Guid stageId, string text, bool isCorrect) => new()
    {
        Id = Guid.NewGuid(),
        StageId = stageId,
        Text = text.Trim(),
        IsCorrect = isCorrect
    };
}
