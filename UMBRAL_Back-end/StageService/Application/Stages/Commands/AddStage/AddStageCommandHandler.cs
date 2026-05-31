namespace StageService.Application.Stages.Commands.AddStage;
using MassTransit;
using MediatR;
using StageService.Domain.Common;
using StageService.Domain.MissionLookup;
using StageService.Domain.Stages;
using UMBRAL.Contracts.Events;

public class AddStageCommandHandler : IRequestHandler<AddStageCommand, Result<Guid>>
{
    private readonly IStageRepository _stageRepository;
    private readonly IMissionLookupRepository _missionLookupRepository;
    private readonly IPublishEndpoint _bus;

    public AddStageCommandHandler(IStageRepository stageRepository, IMissionLookupRepository missionLookupRepository, IPublishEndpoint bus)
    {
        _stageRepository = stageRepository;
        _missionLookupRepository = missionLookupRepository;
        _bus = bus;
    }

    public async Task<Result<Guid>> Handle(AddStageCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionLookupRepository.GetByIdAsync(request.MissionId, cancellationToken);
        // Si el lookup no existe aún (evento no procesado), la misión se asume Inactiva
        if (mission?.IsActive == true) return Result.Failure<Guid>(StageErrors.MissionIsActive);

        if (!Enum.TryParse<StageType>(request.Type, out var stageType))
            return Result.Failure<Guid>(new Error("Stage.InvalidType", $"Unknown stage type: {request.Type}"));

        if (stageType == StageType.TreasureHunt && !string.IsNullOrWhiteSpace(request.QrCode))
            if (await _stageRepository.ExistsWithQrCodeAsync(request.QrCode.Trim(), cancellationToken: cancellationToken))
                return Result.Failure<Guid>(StageErrors.DuplicateQrCode);

        var result = Stage.Create(
            request.MissionId, request.Title, stageType, request.Order, request.BaseScore,
            request.Question, request.Latitude, request.Longitude, request.QrCode);
        if (result.IsFailure) return Result.Failure<Guid>(result.Error);

        var stage = result.Value;
        if (stageType == StageType.Trivia && request.Options is { Count: > 0 })
            stage.ReplaceOptions(request.Options.Select(o => (o.Text, o.IsCorrect)));

        await _stageRepository.AddAsync(stage, cancellationToken);
        await _stageRepository.SaveChangesAsync(cancellationToken);

        await _bus.Publish(new StageAddedIntegrationEvent(stage.Id, request.MissionId, request.Type, DateTime.UtcNow), cancellationToken);

        return Result.Success(stage.Id);
    }
}
