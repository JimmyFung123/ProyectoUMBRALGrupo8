namespace SessionService.Application.Sessions.Commands.ResumeSession;

using MediatR;
using SessionService.Application;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

public class ResumeSessionCommandHandler : IRequestHandler<ResumeSessionCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;

    public ResumeSessionCommandHandler(ISessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    public async Task<Result<bool>> Handle(ResumeSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<bool>(SessionErrors.NotFound);

        var result = session.Resume(request.OperatorName);
        if (result.IsFailure)
            return result;

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
