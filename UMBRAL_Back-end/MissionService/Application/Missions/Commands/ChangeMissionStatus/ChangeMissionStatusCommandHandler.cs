namespace UMBRAL_Back_end.Application.Missions.Commands.ChangeMissionStatus;

using MediatR;
using UMBRAL.Contracts.Events;
using UMBRAL_Back_end.Application;
using UMBRAL_Back_end.Application.Missions;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;

public class ChangeMissionStatusCommandHandler : IRequestHandler<ChangeMissionStatusCommand, Result>
{
    private readonly IMissionRepository _repository;
    private readonly IIntegrationEventBus _bus;
    private readonly IStageCountLookupRepository _stageCountLookupRepository;
    private readonly ISessionServiceClient _sessionServiceClient;

    public ChangeMissionStatusCommandHandler(
        IMissionRepository repository,
        IIntegrationEventBus bus,
        IStageCountLookupRepository stageCountLookupRepository,
        ISessionServiceClient sessionServiceClient)
    {
        _repository = repository;
        _bus = bus;
        _stageCountLookupRepository = stageCountLookupRepository;
        _sessionServiceClient = sessionServiceClient;
    }

    public async Task<Result> Handle(ChangeMissionStatusCommand request, CancellationToken cancellationToken)
    {
        var mission = await _repository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            return Result.Failure(MissionErrors.NotFound);

        if (request.Activate)
        {
            var stageCount = await _stageCountLookupRepository.GetByMissionIdAsync(request.MissionId, cancellationToken);
            if (stageCount is null || !stageCount.HasStages)
                return Result.Failure(MissionErrors.NoStages);
        }

        // RB-15: cross-service check — SessionService owns session lifecycle
        bool hasActiveSessions = await _sessionServiceClient.HasActiveSessionsAsync(request.MissionId, cancellationToken);

        var operationResult = request.Activate
            ? mission.Activate()
            : mission.Deactivate(hasActiveSessions);

        if (operationResult.IsFailure)
            return operationResult;

        await _repository.UpdateAsync(mission, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        if (request.Activate)
        {
            // Integration event → RabbitMQ — SessionService updates its MissionLookup
            await _bus.PublishAsync(
                new MissionActivatedIntegrationEvent(mission.Id, mission.Name, DateTime.UtcNow, mission.Difficulty.ToString()),
                cancellationToken);
        }
        else
        {
            await _bus.PublishAsync(
                new MissionDeactivatedIntegrationEvent(mission.Id, mission.Name, DateTime.UtcNow),
                cancellationToken);
        }

        return Result.Success();
    }
}
