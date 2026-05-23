namespace UMBRAL_Back_end.Application.Missions.Commands.UpdateMission;

using MediatR;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;

public record UpdateMissionCommand(
    Guid MissionId,
    string Name,
    string Description,
    DifficultyLevel Difficulty,
    int MaxDuration
) : IRequest<Result>;
