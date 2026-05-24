namespace SessionService.Application.Sessions.Commands.SubmitTriviaAnswer;

public record TriviaAnswerResultDto(
    bool IsCorrect,
    int NewScore,
    int NextStageOrder,
    bool IsLastStage);
