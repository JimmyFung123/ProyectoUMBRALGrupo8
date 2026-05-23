namespace StageService.Application.Stages.Commands.RemoveStage;

using MassTransit;
using MediatR;
using StageService.Domain.Common;
using StageService.Domain.MissionLookup;
using StageService.Domain.Stages;
using UMBRAL.Contracts.Events;

public class RemoveStageCommandHandler : IRequestHandler<RemoveStageCommand, Result<bool>>
{
    private readonly IStageRepository _stageRepository;
    private readonly IMissionLookupRepository _missionLookupRepository;
    private readonly IPublishEndpoint _bus;

    public RemoveStageCommandHandler(IStageRepository stageRepository, IMissionLookupRepository missionLookupRepository, IPublishEndpoint bus)
    {
        _stageRepository = stageRepository;
        _missionLookupRepository = missionLookupRepository;
        _bus = bus;
    }

    public async Task<Result<bool>> Handle(RemoveStageCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionLookupRepository.GetByIdAsync(request.MissionId, cancellationToken);
        // Si el lookup no existe aún (evento no procesado), la misión se asume Inactiva
        if (mission?.IsActive == true) return Result.Failure<bool>(StageErrors.MissionIsActive);

        var stage = await _stageRepository.GetByIdAsync(request.StageId, cancellationToken);
        if (stage is null) return Result.Failure<bool>(StageErrors.NotFound);

        await _stageRepository.DeleteAsync(stage, cancellationToken);
        await _stageRepository.SaveChangesAsync(cancellationToken);

        await _bus.Publish(new StageRemovedIntegrationEvent(stage.Id, request.MissionId, DateTime.UtcNow), cancellationToken);

        return Result.Success(true);
    }
}
