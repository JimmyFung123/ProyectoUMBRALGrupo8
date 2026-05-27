namespace SessionService.Application.Sessions.Commands.CancelSession;

using MassTransit;
using MediatR;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using UMBRAL.Contracts.Events;

public class CancelSessionCommandHandler : IRequestHandler<CancelSessionCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionEventRepository _eventRepository;
    private readonly IPublishEndpoint _publishEndpoint;

    public CancelSessionCommandHandler(
        ISessionRepository sessionRepository,
        ISessionEventRepository eventRepository,
        IPublishEndpoint publishEndpoint)
    {
        _sessionRepository = sessionRepository;
        _eventRepository = eventRepository;
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

        // HU-22: audit log. Note that HU-22's alternate flow treats Pending/Cancelled
        // sessions as "no events" on the UI — that hides the timeline, but the entry
        // is still persisted for backend traceability.
        var auditEvent = SessionEvent.Create(
            request.SessionId,
            "La sesión fue cancelada.",
            actorName: request.OperatorName,
            commandType: nameof(CancelSessionCommand),
            outcome: SessionEvent.OutcomeSuccess);
        await _eventRepository.AddAsync(auditEvent, cancellationToken);
        await _eventRepository.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish(
            new SessionCancelledIntegrationEvent(request.SessionId, DateTime.UtcNow),
            cancellationToken);

        return Result.Success(true);
    }
}
