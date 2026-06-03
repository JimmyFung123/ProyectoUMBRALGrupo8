namespace SessionService.Application.Sessions.Commands.ResumeSession;

using MediatR;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

public class ResumeSessionCommandHandler : IRequestHandler<ResumeSessionCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionEventRepository _eventRepository;
    private readonly ISessionNotifier _notifier;

    public ResumeSessionCommandHandler(
        ISessionRepository sessionRepository,
        ISessionEventRepository eventRepository,
        ISessionNotifier notifier)
    {
        _sessionRepository = sessionRepository;
        _eventRepository = eventRepository;
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
        var auditEvent = SessionEvent.Create(
            request.SessionId,
            "La sesión fue reanudada.",
            actorName: request.OperatorName,
            commandType: nameof(ResumeSessionCommand),
            outcome: SessionEvent.OutcomeSuccess);
        await _eventRepository.AddAsync(auditEvent, cancellationToken);
        await _eventRepository.SaveChangesAsync(cancellationToken);

        await _notifier.NotifyStateChangedAsync(session.Id, session.Status.ToString(), cancellationToken);

        return Result.Success(true);
    }
}
