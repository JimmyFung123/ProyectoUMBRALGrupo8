namespace ClueService.Application.Clues.Queries.GetCluesByStage;
using MediatR;
using ClueService.Domain.Clues;
public class GetCluesByStageQueryHandler : IRequestHandler<GetCluesByStageQuery, List<ClueDto>>
{
    private readonly IClueRepository _repository;
    public GetCluesByStageQueryHandler(IClueRepository repository) => _repository = repository;

    public async Task<List<ClueDto>> Handle(GetCluesByStageQuery request, CancellationToken cancellationToken)
    {
        var clues = await _repository.GetByStageIdAsync(request.StageId, cancellationToken);
        return clues.Select(c => new ClueDto(c.Id, c.StageId, c.MissionId, c.Content, c.Order, c.CreatedAt)).ToList();
    }
}
