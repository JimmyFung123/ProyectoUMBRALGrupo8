namespace UMBRAL_Back_end.Application.Missions.Commands.ChangeMissionStatus;

using MediatR;
using UMBRAL_Back_end.Domain.Common;

public record ChangeMissionStatusCommand(
    Guid MissionId,
    bool Activate
) : IRequest<Result>;
