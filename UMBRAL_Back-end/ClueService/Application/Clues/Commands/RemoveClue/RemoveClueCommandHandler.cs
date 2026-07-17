namespace ClueService.Application.Clues.Commands.RemoveClue;
using MediatR;
using ClueService.Application;
using ClueService.Domain.Clues;
using ClueService.Domain.Common;

public class RemoveClueCommandHandler : IRequestHandler<RemoveClueCommand, Result<bool>>
{
    private readonly IClueRepository _clueRepository;

    public RemoveClueCommandHandler(IClueRepository clueRepository)
    {
        _clueRepository = clueRepository;
    }

    public async Task<Result<bool>> Handle(RemoveClueCommand request, CancellationToken cancellationToken)
    {
        var clue = await _clueRepository.GetByIdAsync(request.ClueId, cancellationToken);
        if (clue is null) return Result.Failure<bool>(ClueErrors.NotFound);

        clue.MarkForRemoval();
        await _clueRepository.DeleteAsync(clue, cancellationToken);
        await _clueRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
