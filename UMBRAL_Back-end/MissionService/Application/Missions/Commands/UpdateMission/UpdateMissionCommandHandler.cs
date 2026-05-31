namespace UMBRAL_Back_end.Application.Missions.Commands.UpdateMission;

using MediatR;
using UMBRAL_Back_end.Application.Missions;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Domain.Missions.Events;

public class UpdateMissionCommandHandler : IRequestHandler<UpdateMissionCommand, Result>
{
    private readonly IMissionRepository _repository;
    private readonly IPublisher _publisher;
    private readonly ISessionServiceClient _sessionServiceClient;

    public UpdateMissionCommandHandler(
        IMissionRepository repository,
        IPublisher publisher,
        ISessionServiceClient sessionServiceClient)
    {
        _repository = repository;
        _publisher = publisher;
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

        await _publisher.Publish(
            new MissionUpdatedEvent(mission.Id, mission.Name, mission.Difficulty.ToString(), mission.MaxDuration, mission.UpdatedAt!.Value),
            cancellationToken);

        return Result.Success();
    }
}
