namespace UMBRAL_Back_end.Application.Missions.Queries.GetCluesByStage;

using MediatR;
using UMBRAL_Back_end.Domain.Common;

public record GetCluesByStageQuery(Guid MissionId, Guid StageId) : IRequest<Result<IReadOnlyList<ClueDto>>>;
