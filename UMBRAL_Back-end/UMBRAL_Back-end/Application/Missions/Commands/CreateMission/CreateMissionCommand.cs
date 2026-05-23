namespace UMBRAL_Back_end.Application.Missions.Commands.CreateMission;

using MediatR;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;

public record CreateMissionCommand(
    string Name,
    string Description,
    DifficultyLevel Difficulty,
    int MaxDuration
) : IRequest<Result<Guid>>;
