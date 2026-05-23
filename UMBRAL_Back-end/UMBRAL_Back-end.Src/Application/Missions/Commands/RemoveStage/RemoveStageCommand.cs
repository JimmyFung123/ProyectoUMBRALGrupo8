namespace UMBRAL_Back_end.Application.Missions.Commands.RemoveStage;

using MediatR;
using UMBRAL_Back_end.Domain.Common;

public record RemoveStageCommand(Guid MissionId, Guid StageId) : IRequest<Result>;
