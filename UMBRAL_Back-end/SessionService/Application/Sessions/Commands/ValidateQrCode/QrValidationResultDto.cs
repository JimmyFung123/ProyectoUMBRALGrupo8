namespace SessionService.Application.Sessions.Commands.ValidateQrCode;

public record QrValidationResultDto(
    bool IsCorrect,
    int NewScore,
    int NextStageOrder,
    bool IsLastStage);
