namespace UMBRAL_Back_end.Application.Missions.Commands.UpdateMission;

using MassTransit;
using MediatR;
using UMBRAL.Contracts.Events;
using UMBRAL_Back_end.Application.Missions;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Domain.Missions.Events;

public class UpdateMissionCommandHandler : IRequestHandler<UpdateMissionCommand, Result>
{
    private readonly IMissionRepository _repository;
    private readonly IPublisher _publisher;
    private readonly IPublishEndpoint _bus;
    private readonly ISessionServiceClient _sessionServiceClient;

    public UpdateMissionCommandHandler(
        IMissionRepository repository,
        IPublisher publisher,
        IPublishEndpoint bus,
        ISessionServiceClient sessionServiceClient)
    {
        _repository = repository;
        _publisher = publisher;
        _bus = bus;
        _sessionServiceClient = sessionServiceClient;
    }

    public async Task<Result> Handle(UpdateMissionCommand request, CancellationToken cancellationToken)
    {
        var mission = await _repository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            return Result.Failure(MissionErrors.NotFound);

        bool nameConflict = await _repository.ExistsWithNameAsync(request.Name, request.MissionId, cancellationToken);
        if (nameConflict)
            return Result.Failure(MissionErrors.DuplicateName);

        bool hasActiveSessions = await _sessionServiceClient.HasActiveSessionsAsync(request.MissionId, cancellationToken);

        var updateResult = mission.Update(request.Name, request.Description, request.Difficulty, request.MaxDuration, hasActiveSessions);
        if (updateResult.IsFailure)
            return updateResult;

        await _repository.UpdateAsync(mission, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        // In-process domain event
        await _publisher.Publish(
            new MissionUpdatedEvent(mission.Id, mission.Name, mission.Difficulty.ToString(), mission.MaxDuration, mission.UpdatedAt!.Value),
            cancellationToken);

        // Integration event — SessionService updates its MissionLookup replica
        // (specifically the Difficulty field used by IScoringStrategy)
        await _bus.Publish(
            new MissionUpdatedIntegrationEvent(mission.Id, mission.Name, mission.Difficulty.ToString(), DateTime.UtcNow),
            cancellationToken);

        return Result.Success();
    }
}
