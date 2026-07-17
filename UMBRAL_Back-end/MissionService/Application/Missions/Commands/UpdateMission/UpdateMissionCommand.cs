namespace UMBRAL_Back_end.Application.Missions.Commands.UpdateMission;

using MediatR;
using UMBRAL_Back_end.Domain.Common;

public record UpdateMissionCommand(
    Guid MissionId,
    string Name,
    string Description,
    string Difficulty,
    int MaxDuration
) : IRequest<Result>;
