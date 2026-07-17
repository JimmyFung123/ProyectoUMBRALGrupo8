namespace UMBRAL_Back_end.Application.Missions.Commands.ChangeMissionStatus;

using MediatR;
using UMBRAL_Back_end.Application;
using UMBRAL_Back_end.Application.Missions;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;

public class ChangeMissionStatusCommandHandler : IRequestHandler<ChangeMissionStatusCommand, Result>
{
    private readonly IMissionRepository _repository;
    private readonly IStageCountLookupRepository _stageCountLookupRepository;
    private readonly ISessionServiceClient _sessionServiceClient;

    public ChangeMissionStatusCommandHandler(
        IMissionRepository repository,
        IStageCountLookupRepository stageCountLookupRepository,
        ISessionServiceClient sessionServiceClient)
    {
        _repository = repository;
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

        return Result.Success();
    }
}
