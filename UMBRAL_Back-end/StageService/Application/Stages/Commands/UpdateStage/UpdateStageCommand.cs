namespace StageService.Application.Stages.Commands.UpdateStage;
using MediatR;
using StageService.Domain.Common;

public record UpdateStageCommand(
    Guid StageId,
    string Title,
    int Order,
    int BaseScore,
    string? Question,
    List<(string Text, bool IsCorrect)>? Options,
    double? Latitude,
    double? Longitude,
    string? QrCode,
    int? AutoReleaseTimeMinutes,
    int? AutoReleaseMaxAttempts
) : IRequest<Result<bool>>;
