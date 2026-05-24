namespace ClueService.Application.Clues.Commands.AddClue;
using MassTransit;
using MediatR;
using ClueService.Domain.Clues;
using ClueService.Domain.Common;
using ClueService.Domain.StageLookup;
using UMBRAL.Contracts.Events;

public class AddClueCommandHandler : IRequestHandler<AddClueCommand, Result<Guid>>
{
    private readonly IClueRepository _clueRepository;
    private readonly IStageLookupRepository _stageLookupRepository;
    private readonly IPublishEndpoint _bus;

    public AddClueCommandHandler(IClueRepository clueRepository, IStageLookupRepository stageLookupRepository, IPublishEndpoint bus)
    {
        _clueRepository = clueRepository;
        _stageLookupRepository = stageLookupRepository;
        _bus = bus;
    }

    public async Task<Result<Guid>> Handle(AddClueCommand request, CancellationToken cancellationToken)
    {
        var stage = await _stageLookupRepository.GetByIdAsync(request.StageId, cancellationToken);
        if (stage is null) return Result.Failure<Guid>(ClueErrors.StageNotFound);

        var existing = await _clueRepository.GetByStageIdAsync(request.StageId, cancellationToken);
        var result = Clue.Create(request.StageId, stage.MissionId, request.Content, existing.Count + 1, request.AutoReleaseAfterMinutes);
        if (result.IsFailure) return Result.Failure<Guid>(result.Error);

        await _clueRepository.AddAsync(result.Value, cancellationToken);
        await _clueRepository.SaveChangesAsync(cancellationToken);

        await _bus.Publish(
            new ClueAddedIntegrationEvent(result.Value.Id, request.StageId, stage.MissionId, request.Content, DateTime.UtcNow),
            cancellationToken);

        return Result.Success(result.Value.Id);
    }
}
