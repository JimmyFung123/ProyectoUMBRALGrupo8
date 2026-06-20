namespace SessionService.Application.Sessions.Commands.CancelSession;

using MediatR;
using SessionService.Application;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using UMBRAL.Contracts.Events;

public class CancelSessionCommandHandler : IRequestHandler<CancelSessionCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IIntegrationEventBus _bus;

    public CancelSessionCommandHandler(
        ISessionRepository sessionRepository,
        IIntegrationEventBus bus)
    {
        _sessionRepository = sessionRepository;
        _bus = bus;
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

        // HU-22: audit log. Note that HU-22's alternate flow treats Pending/Cancelled
        // sessions as "no events" on the UI — that hides the timeline, but the entry
        // is still persisted for backend traceability.
        await _bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                request.SessionId,
                "La sesión fue cancelada.",
                ActorName: request.OperatorName,
                CommandType: nameof(CancelSessionCommand),
                Outcome: SessionEvent.OutcomeSuccess,
                DateTime.UtcNow),
            cancellationToken);

        await _bus.PublishAsync(
            new SessionCancelledIntegrationEvent(request.SessionId, DateTime.UtcNow),
            cancellationToken);

        return Result.Success(true);
    }
}
