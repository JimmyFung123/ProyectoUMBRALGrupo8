namespace UMBRAL_Back_end.Application.Missions.Commands.UpdateMission;

using MediatR;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;

public class UpdateMissionCommandHandler : IRequestHandler<UpdateMissionCommand, Result>
{
    private readonly IMissionRepository _repository;

    public UpdateMissionCommandHandler(IMissionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(UpdateMissionCommand request, CancellationToken cancellationToken)
    {
        var mission = await _repository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            return Result.Failure(MissionErrors.NotFound);

        bool nameConflict = await _repository.ExistsWithNameAsync(request.Name, request.MissionId, cancellationToken);
        if (nameConflict)
            return Result.Failure(MissionErrors.DuplicateName);

        bool hasActiveSessions = await _repository.HasActiveSessionsAsync(request.MissionId, cancellationToken);

        var updateResult = mission.Update(request.Name, request.Description, request.Difficulty, request.MaxDuration, hasActiveSessions);
        if (updateResult.IsFailure)
            return updateResult;

        await _repository.UpdateAsync(mission, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
