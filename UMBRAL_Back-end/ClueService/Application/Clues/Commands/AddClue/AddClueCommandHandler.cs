namespace ClueService.Application.Clues.Commands.AddClue;
using MediatR;
using ClueService.Application;
using ClueService.Domain.Clues;
using ClueService.Domain.Common;
using ClueService.Domain.StageLookup;

public class AddClueCommandHandler : IRequestHandler<AddClueCommand, Result<Guid>>
{
    private readonly IClueRepository _clueRepository;
    private readonly IStageLookupRepository _stageLookupRepository;

    public AddClueCommandHandler(IClueRepository clueRepository, IStageLookupRepository stageLookupRepository)
    {
        _clueRepository = clueRepository;
        _stageLookupRepository = stageLookupRepository;
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

        return Result.Success(clue.Id);
    }
}
