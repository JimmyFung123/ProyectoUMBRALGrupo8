namespace StageService.Application.Stages.Commands.RemoveStage;

using MediatR;
using StageService.Application;
using StageService.Domain.Common;
using StageService.Domain.MissionLookup;
using StageService.Domain.Stages;
using UMBRAL.Contracts.Events;

public class RemoveStageCommandHandler : IRequestHandler<RemoveStageCommand, Result<bool>>
{
    private readonly IStageRepository _stageRepository;
    private readonly IMissionLookupRepository _missionLookupRepository;
    private readonly IIntegrationEventBus _bus;

    public RemoveStageCommandHandler(IStageRepository stageRepository, IMissionLookupRepository missionLookupRepository, IIntegrationEventBus bus)
    {
        _stageRepository = stageRepository;
        _missionLookupRepository = missionLookupRepository;
        _bus = bus;
    }

    public async Task<Result<bool>> Handle(RemoveStageCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionLookupRepository.GetByIdAsync(request.MissionId, cancellationToken);
        if (StageMissionActivePolicy.BlocksStageMutation(mission)) return Result.Failure<bool>(StageErrors.MissionIsActive);

        var stage = await _stageRepository.GetByIdAsync(request.StageId, cancellationToken);
        if (stage is null) return Result.Failure<bool>(StageErrors.NotFound);

        await _stageRepository.DeleteAsync(stage, cancellationToken);
        await _stageRepository.SaveChangesAsync(cancellationToken);

        await _bus.PublishAsync(new StageRemovedIntegrationEvent(stage.Id, request.MissionId, DateTime.UtcNow), cancellationToken);

        return Result.Success(true);
    }
}
