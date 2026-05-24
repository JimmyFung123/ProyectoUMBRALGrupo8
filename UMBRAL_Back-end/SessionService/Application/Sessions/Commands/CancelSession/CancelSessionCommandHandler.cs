namespace SessionService.Application.Sessions.Commands.CancelSession;

using MassTransit;
using MediatR;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using UMBRAL.Contracts.Events;

public class CancelSessionCommandHandler : IRequestHandler<CancelSessionCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IPublishEndpoint _publishEndpoint;

    public CancelSessionCommandHandler(
        ISessionRepository sessionRepository,
        IPublishEndpoint publishEndpoint)
    {
        _sessionRepository = sessionRepository;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<Result<bool>> Handle(CancelSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<bool>(SessionErrors.NotFound);

        var cancelResult = session.Cancel();
        if (cancelResult.IsFailure)
            return cancelResult;

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish(
            new SessionCancelledIntegrationEvent(request.SessionId, DateTime.UtcNow),
            cancellationToken);

        return Result.Success(true);
    }
}
