namespace SessionService.Application.Sessions.Commands.ResumeSession;

using MediatR;
using SessionService.Application;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using UMBRAL.Contracts.Events;

public class ResumeSessionCommandHandler : IRequestHandler<ResumeSessionCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IIntegrationEventBus _bus;
    private readonly ISessionNotifier _notifier;

    public ResumeSessionCommandHandler(
        ISessionRepository sessionRepository,
        IIntegrationEventBus bus,
        ISessionNotifier notifier)
    {
        _sessionRepository = sessionRepository;
        _bus = bus;
        _notifier = notifier;
    }

    public async Task<Result<bool>> Handle(ResumeSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<bool>(SessionErrors.NotFound);

        var result = session.Resume();
        if (result.IsFailure)
            return result;

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        // HU-22 / HU-26: audit log
        await _bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                request.SessionId,
                "La sesión fue reanudada.",
                ActorName: request.OperatorName,
                CommandType: nameof(ResumeSessionCommand),
                Outcome: SessionEvent.OutcomeSuccess,
                DateTime.UtcNow),
            cancellationToken);

        await _notifier.NotifyStateChangedAsync(session.Id, session.Status.ToString(), cancellationToken);

        return Result.Success(true);
    }
}
