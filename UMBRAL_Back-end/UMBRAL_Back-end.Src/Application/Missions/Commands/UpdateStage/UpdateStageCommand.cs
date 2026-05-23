namespace UMBRAL_Back_end.Application.Missions.Commands.UpdateStage;

using MediatR;
using UMBRAL_Back_end.Application.Missions.Commands.AddStageToMission;
using UMBRAL_Back_end.Domain.Common;

public record UpdateStageCommand(
    Guid MissionId,
    Guid StageId,
    string Title,
    int Order,
    int BaseScore,
    string? Question,
    IReadOnlyList<TriviaOptionInput>? Options,
    double? Latitude,
    double? Longitude,
    string? QrCode
) : IRequest<Result>;
