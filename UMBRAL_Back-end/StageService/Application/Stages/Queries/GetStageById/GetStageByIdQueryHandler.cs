namespace StageService.Application.Stages.Queries.GetStageById;

using MediatR;
using StageService.Application.Stages.Queries.GetStagesByMission;
using StageService.Domain.Common;
using StageService.Domain.Stages;

public class GetStageByIdQueryHandler : IRequestHandler<GetStageByIdQuery, Result<StageDto>>
{
    private readonly IStageRepository _repository;

    public GetStageByIdQueryHandler(IStageRepository repository)
        => _repository = repository;

    public async Task<Result<StageDto>> Handle(GetStageByIdQuery request, CancellationToken cancellationToken)
    {
        var stage = await _repository.GetByIdAsync(request.StageId, cancellationToken);
        if (stage is null)
            return Result.Failure<StageDto>(StageErrors.NotFound);

        return Result.Success(StageMapper.ToDto(stage));
    }
}
