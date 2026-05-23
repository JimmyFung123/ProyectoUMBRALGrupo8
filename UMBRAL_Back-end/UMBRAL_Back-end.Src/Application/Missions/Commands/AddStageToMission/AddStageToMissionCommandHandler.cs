namespace UMBRAL_Back_end.Application.Missions.Commands.AddStageToMission;

using MediatR;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;

public class AddStageToMissionCommandHandler : IRequestHandler<AddStageToMissionCommand, Result<Guid>>
{
    private readonly IMissionRepository _repository;

    public AddStageToMissionCommandHandler(IMissionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<Guid>> Handle(AddStageToMissionCommand request, CancellationToken cancellationToken)
    {
        var mission = await _repository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            return Result.Failure<Guid>(MissionErrors.NotFound);

        // Check QR uniqueness before touching the aggregate (TreasureHunt only)
        if (request.Type == StageType.TreasureHunt && !string.IsNullOrWhiteSpace(request.QrCode))
        {
            bool qrExists = await _repository.HasDuplicateQrCodeAsync(request.QrCode, cancellationToken: cancellationToken);
            if (qrExists)
                return Result.Failure<Guid>(StageErrors.DuplicateQrCode);
        }

        bool hasActiveSessions = await _repository.HasActiveSessionsAsync(request.MissionId, cancellationToken);

        var options = request.Options?
            .Select(o => (o.Text, o.IsCorrect))
            .ToList();

        var addResult = mission.AddStage(
            title: request.Title,
            order: request.Order,
            type: request.Type,
            baseScore: request.BaseScore,
            hasActiveSessions: hasActiveSessions,
            question: request.Question,
            options: options,
            latitude: request.Latitude,
            longitude: request.Longitude,
            qrCode: request.QrCode);

        if (addResult.IsFailure)
            return Result.Failure<Guid>(addResult.Error);

        // Explicitly register the new stage so EF Core tracks it as Added,
        // regardless of change-tracking behavior for private backing fields.
        await _repository.AddStageAsync(addResult.Value, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result.Success(addResult.Value.Id);
    }
}
