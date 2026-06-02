namespace UMBRAL_Back_end.Application.Missions.Commands.CreateMission;

using MediatR;
using UMBRAL.Contracts.Events;
using UMBRAL_Back_end.Application;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;

public class CreateMissionCommandHandler : IRequestHandler<CreateMissionCommand, Result<Guid>>
{
    private readonly IMissionRepository _repository;
    private readonly IIntegrationEventBus _bus;

    public CreateMissionCommandHandler(IMissionRepository repository, IIntegrationEventBus bus)
    {
        _repository = repository;
        _bus = bus;
    }

    public async Task<Result<Guid>> Handle(CreateMissionCommand request, CancellationToken cancellationToken)
    {
        if (await _repository.ExistsWithNameAsync(request.Name, cancellationToken: cancellationToken))
            return Result.Failure<Guid>(MissionErrors.DuplicateName);

        if (!Enum.TryParse<DifficultyLevel>(request.Difficulty, ignoreCase: true, out var difficulty))
            return Result.Failure<Guid>(MissionErrors.InvalidDifficulty);

        var missionResult = Mission.Create(request.Name, request.Description, difficulty, request.MaxDuration);
        if (missionResult.IsFailure)
            return Result.Failure<Guid>(missionResult.Error);

        var mission = missionResult.Value;

        await _repository.AddAsync(mission, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        // Integration event → RabbitMQ — consumed by SessionService
        await _bus.PublishAsync(
            new MissionCreatedIntegrationEvent(mission.Id, mission.Name, "Inactive", mission.CreatedAt),
            cancellationToken);

        return Result.Success(mission.Id);
    }
}
