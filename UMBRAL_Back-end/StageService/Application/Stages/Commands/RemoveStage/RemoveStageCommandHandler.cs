namespace StageService.Application.Stages.Commands.RemoveStage;

using MediatR;
using StageService.Application;
using StageService.Domain.Common;
using StageService.Domain.MissionLookup;
using StageService.Domain.Stages;

public class RemoveStageCommandHandler : IRequestHandler<RemoveStageCommand, Result<bool>>
{
    private readonly IStageRepository _stageRepository;
    private readonly IMissionLookupRepository _missionLookupRepository;

    public RemoveStageCommandHandler(IStageRepository stageRepository, IMissionLookupRepository missionLookupRepository)
    {
        _stageRepository = stageRepository;
        _missionLookupRepository = missionLookupRepository;
    }

    public async Task<Result<bool>> Handle(RemoveStageCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionLookupRepository.GetByIdAsync(request.MissionId, cancellationToken);
        if (StageMissionActivePolicy.BlocksStageMutation(mission)) return Result.Failure<bool>(StageErrors.MissionIsActive);

        var stage = await _stageRepository.GetByIdAsync(request.StageId, cancellationToken);
        if (stage is null) return Result.Failure<bool>(StageErrors.NotFound);

        stage.MarkForRemoval();
        await _stageRepository.DeleteAsync(stage, cancellationToken);
        await _stageRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
