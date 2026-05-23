namespace SessionService.Application.Sessions.Commands.CancelSession;

using MediatR;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

public class CancelSessionCommandHandler : IRequestHandler<CancelSessionCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamRepository _teamRepository;

    public CancelSessionCommandHandler(
        ISessionRepository sessionRepository,
        ITeamRepository teamRepository)
    {
        _sessionRepository = sessionRepository;
        _teamRepository = teamRepository;
    }

    public async Task<Result<bool>> Handle(CancelSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<bool>(SessionErrors.NotFound);

        var cancelResult = session.Cancel();
        if (cancelResult.IsFailure)
            return cancelResult;

        // Remove all enrolled teams from the session
        // (WebSocket notification to participants will be added in HU-11)
        await _teamRepository.DeleteBySessionIdAsync(request.SessionId, cancellationToken);

        await _sessionRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
