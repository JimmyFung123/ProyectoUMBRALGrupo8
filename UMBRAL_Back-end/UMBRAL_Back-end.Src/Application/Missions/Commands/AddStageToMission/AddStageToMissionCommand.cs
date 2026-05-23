namespace UMBRAL_Back_end.Application.Missions.Commands.AddStageToMission;

using MediatR;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;

public record AddStageToMissionCommand(
    Guid MissionId,
    string Title,
    int Order,
    StageType Type,
    int BaseScore,

    // Trivia fields (optional for TreasureHunt)
    string? Question,
    IReadOnlyList<TriviaOptionInput>? Options,

    // TreasureHunt fields — RB-20: both required when Type = TreasureHunt
    double? Latitude,
    double? Longitude,
    string? QrCode
) : IRequest<Result<Guid>>;

public record TriviaOptionInput(string Text, bool IsCorrect);
