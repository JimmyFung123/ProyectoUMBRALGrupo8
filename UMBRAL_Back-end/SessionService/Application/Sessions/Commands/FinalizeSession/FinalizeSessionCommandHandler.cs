namespace SessionService.Application.Sessions.Commands.FinalizeSession;

using MediatR;
using SessionService.Application;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using SessionService.Domain.Statistics;

public class FinalizeSessionCommandHandler : IRequestHandler<FinalizeSessionCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IStageCompletionRecordRepository _statsRepository;

    public FinalizeSessionCommandHandler(
        ISessionRepository sessionRepository,
        IStageCompletionRecordRepository statsRepository)
    {
        _sessionRepository = sessionRepository;
        _statsRepository = statsRepository;
    }

    public async Task<Result<bool>> Handle(FinalizeSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<bool>(SessionErrors.NotFound);

        var result = session.Finalize(request.OperatorName);
        if (result.IsFailure)
            return result;

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        // HU-25: promote every stage-completion record of this session so it
        // shows up on the admin dashboard. Single SQL UPDATE — irrelevant
        // load even for sessions with hundreds of stage transitions.
        await _statsRepository.MarkSessionIncludedAsync(request.SessionId, cancellationToken);

        return Result.Success(true);
    }
}
