namespace UMBRAL_Back_end.Application.Missions.Commands.ConfigureAutoReleaseRule;

using MediatR;
using UMBRAL_Back_end.Domain.Common;

public record ConfigureAutoReleaseRuleCommand(
    Guid MissionId,
    Guid StageId,
    int? TimeMinutes,
    int? MaxAttempts
) : IRequest<Result>;
