namespace ClueService.Application.Clues.Commands.RemoveClue;
using MediatR;
using ClueService.Application;
using ClueService.Domain.Clues;
using ClueService.Domain.Common;
using UMBRAL.Contracts.Events;

public class RemoveClueCommandHandler : IRequestHandler<RemoveClueCommand, Result<bool>>
{
    private readonly IClueRepository _clueRepository;
    private readonly IIntegrationEventBus _bus;

    public RemoveClueCommandHandler(IClueRepository clueRepository, IIntegrationEventBus bus)
    {
        _clueRepository = clueRepository;
        _bus = bus;
    }

    public async Task<Result<bool>> Handle(RemoveClueCommand request, CancellationToken cancellationToken)
    {
        var clue = await _clueRepository.GetByIdAsync(request.ClueId, cancellationToken);
        if (clue is null) return Result.Failure<bool>(ClueErrors.NotFound);

        await _clueRepository.DeleteAsync(clue, cancellationToken);
        await _clueRepository.SaveChangesAsync(cancellationToken);

        await _bus.PublishAsync(
            new ClueRemovedIntegrationEvent(clue.Id, clue.StageId, clue.MissionId, DateTime.UtcNow),
            cancellationToken);

        return Result.Success(true);
    }
}
