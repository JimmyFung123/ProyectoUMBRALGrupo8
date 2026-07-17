namespace UMBRAL_Back_end.Application.Missions.Commands.CreateMission;

using MediatR;
using UMBRAL_Back_end.Domain.Common;

public record CreateMissionCommand(
    string Name,
    string Description,
    string Difficulty,
    int MaxDuration
) : IRequest<Result<Guid>>;
