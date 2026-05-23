namespace UMBRAL_Back_end.Application.Missions.Commands.CreateMission;

using MediatR;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Domain.Missions.Events;

public class CreateMissionCommandHandler : IRequestHandler<CreateMissionCommand, Result<Guid>>
{
    private readonly IMissionRepository _repository;
    private readonly IPublisher _publisher;

    public CreateMissionCommandHandler(IMissionRepository repository, IPublisher publisher)
    {
        _repository = repository;
        _publisher = publisher;
    }

    public async Task<Result<Guid>> Handle(CreateMissionCommand request, CancellationToken cancellationToken)
    {
        if (await _repository.ExistsWithNameAsync(request.Name, cancellationToken: cancellationToken))
            return Result.Failure<Guid>(MissionErrors.DuplicateName);

        var missionResult = Mission.Create(request.Name, request.Description, request.Difficulty, request.MaxDuration);
        if (missionResult.IsFailure)
            return Result.Failure<Guid>(missionResult.Error);

        var mission = missionResult.Value;

        await _repository.AddAsync(mission, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(
            new MissionCreatedEvent(mission.Id, mission.Name, mission.Difficulty.ToString(), mission.MaxDuration, mission.CreatedAt),
            cancellationToken);

        return Result.Success(mission.Id);
    }
}
