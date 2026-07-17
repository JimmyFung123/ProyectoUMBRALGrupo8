namespace ClueService.Application.Clues.Queries.GetCluesByStage;
using MediatR;
public record GetCluesByStageQuery(Guid StageId) : IRequest<List<ClueDto>>;
