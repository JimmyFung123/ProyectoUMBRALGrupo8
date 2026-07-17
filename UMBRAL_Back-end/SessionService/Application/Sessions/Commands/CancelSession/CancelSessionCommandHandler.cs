namespace SessionService.Application.Sessions.Commands.CancelSession;

using MediatR;
using SessionService.Application;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

public class CancelSessionCommandHandler : IRequestHandler<CancelSessionCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;

    public CancelSessionCommandHandler(ISessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    public async Task<Result<bool>> Handle(CancelSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<bool>(SessionErrors.NotFound);

        var cancelResult = session.Cancel(request.OperatorName);
        if (cancelResult.IsFailure)
            return cancelResult;

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
