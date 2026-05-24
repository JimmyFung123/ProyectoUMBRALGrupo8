namespace StageService.Application.Stages.Queries.GetStageById;

using MediatR;
using StageService.Application.Stages.Queries.GetStagesByMission;
using StageService.Domain.Common;

public record GetStageByIdQuery(Guid StageId) : IRequest<Result<StageDto>>;
