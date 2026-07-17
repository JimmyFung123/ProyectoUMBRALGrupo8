namespace StageService.Application.Stages.Commands.RemoveStage;
using MediatR;
using StageService.Domain.Common;

public record RemoveStageCommand(Guid StageId, Guid MissionId) : IRequest<Result<bool>>;
