namespace StageService.Application.Stages.Commands.SetAutoRelease;

using MediatR;
using StageService.Domain.Common;
using StageService.Domain.Stages;

public class SetAutoReleaseCommandHandler : IRequestHandler<SetAutoReleaseCommand, Result<bool>>
{
    private readonly IStageRepository _repository;
    public SetAutoReleaseCommandHandler(IStageRepository repository) => _repository = repository;

    public async Task<Result<bool>> Handle(SetAutoReleaseCommand request, CancellationToken cancellationToken)
    {
        var stage = await _repository.GetByIdAsync(request.StageId, cancellationToken);
        if (stage is null) return Result.Failure<bool>(StageErrors.NotFound);

        stage.SetAutoRelease(request.TimeMinutes, request.MaxAttempts);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
