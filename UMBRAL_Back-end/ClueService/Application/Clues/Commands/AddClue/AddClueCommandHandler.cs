namespace ClueService.Application.Clues.Commands.AddClue;
using MediatR;
using ClueService.Application;
using ClueService.Domain.Clues;
using ClueService.Domain.Common;
using ClueService.Domain.StageLookup;
using UMBRAL.Contracts.Events;

public class AddClueCommandHandler : IRequestHandler<AddClueCommand, Result<Guid>>
{
    private readonly IClueRepository _clueRepository;
    private readonly IStageLookupRepository _stageLookupRepository;
    private readonly IIntegrationEventBus _bus;

    public AddClueCommandHandler(IClueRepository clueRepository, IStageLookupRepository stageLookupRepository, IIntegrationEventBus bus)
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
        // Respect the order the operator provided, falling back to the next sequential slot.
        var order = request.Order > 0 ? request.Order : existing.Count + 1;

        var result = Clue.Create(
            request.StageId,
            stage.MissionId,
            stage.Name,
            order,
            request.Content,
            request.Latitude,
            request.Longitude,
            request.RadiusMeters,
            request.AutoReleaseAfterMinutes);
        if (result.IsFailure) return Result.Failure<Guid>(result.Error);

        var clue = result.Value;
        await _clueRepository.AddAsync(clue, cancellationToken);
        await _clueRepository.SaveChangesAsync(cancellationToken);

        await _bus.PublishAsync(
            new ClueAddedIntegrationEvent(
                clue.Id, clue.StageId, clue.MissionId,
                clue.Content, clue.Latitude, clue.Longitude, clue.RadiusMeters,
                DateTime.UtcNow),
            cancellationToken);

        return Result.Success(clue.Id);
    }
}
