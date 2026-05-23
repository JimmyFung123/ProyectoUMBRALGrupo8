namespace StageService.Application.Stages.Commands.AddStage;
using MediatR;
using StageService.Domain.Common;
public record AddStageCommand(
    Guid MissionId,
    string Title,
    string Type,
    int Order,
    int BaseScore,
    string? Question,
    List<(string Text, bool IsCorrect)>? Options,
    double? Latitude,
    double? Longitude,
    string? QrCode
) : IRequest<Result<Guid>>;
