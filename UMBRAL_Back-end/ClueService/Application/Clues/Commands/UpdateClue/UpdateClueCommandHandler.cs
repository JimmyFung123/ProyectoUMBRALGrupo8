namespace ClueService.Application.Clues.Commands.UpdateClue;
using MediatR;
using ClueService.Domain.Clues;
using ClueService.Domain.Common;

public class UpdateClueCommandHandler : IRequestHandler<UpdateClueCommand, Result<bool>>
{
    private readonly IClueRepository _clueRepository;

    public UpdateClueCommandHandler(IClueRepository clueRepository)
    {
        _clueRepository = clueRepository;
    }

    public async Task<Result<bool>> Handle(UpdateClueCommand request, CancellationToken cancellationToken)
    {
        var clue = await _clueRepository.GetByIdAsync(request.ClueId, cancellationToken);
        if (clue is null) return Result.Failure<bool>(ClueErrors.NotFound);

        var update = clue.Update(request.Order, request.Content, request.Latitude, request.Longitude, request.RadiusMeters);
        if (update.IsFailure) return update;

        clue.SetAutoRelease(request.AutoReleaseAfterMinutes);
        await _clueRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
