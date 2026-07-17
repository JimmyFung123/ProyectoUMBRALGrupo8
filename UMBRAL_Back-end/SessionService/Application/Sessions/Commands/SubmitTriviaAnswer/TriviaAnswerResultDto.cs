namespace SessionService.Application.Sessions.Commands.SubmitTriviaAnswer;

public record TriviaAnswerResultDto(
    bool IsCorrect,
    int NewScore,
    int NextStageOrder,
    bool IsLastStage,
    bool ShouldAdvance = true,
    Guid? BlockedOptionId = null,
    int AttemptsUsed = 0,
    int MaxAttempts = 0);
