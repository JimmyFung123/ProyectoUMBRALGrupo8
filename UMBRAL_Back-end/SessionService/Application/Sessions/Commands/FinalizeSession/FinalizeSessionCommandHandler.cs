namespace SessionService.Application.Sessions.Commands.FinalizeSession;

using MediatR;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using SessionService.Domain.Statistics;

public class FinalizeSessionCommandHandler : IRequestHandler<FinalizeSessionCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionEventRepository _eventRepository;
    private readonly IStageCompletionRecordRepository _statsRepository;
    private readonly ISessionNotifier _notifier;

    public FinalizeSessionCommandHandler(
        ISessionRepository sessionRepository,
        ISessionEventRepository eventRepository,
        IStageCompletionRecordRepository statsRepository,
        ISessionNotifier notifier)
    {
        _sessionRepository = sessionRepository;
        _eventRepository = eventRepository;
        _statsRepository = statsRepository;
        _notifier = notifier;
    }

    public async Task<Result<bool>> Handle(FinalizeSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<bool>(SessionErrors.NotFound);

        var result = session.Finalize();
        if (result.IsFailure)
            return result;

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        // HU-25: promote every stage-completion record of this session so it
        // shows up on the admin dashboard. Single SQL UPDATE — irrelevant
        // load even for sessions with hundreds of stage transitions.
        await _statsRepository.MarkSessionIncludedAsync(request.SessionId, cancellationToken);

        // HU-22 / HU-26: audit log (irreversible state — last entry on the timeline)
        var auditEvent = SessionEvent.Create(
            request.SessionId,
            "La sesión fue finalizada. Ranking definitivo calculado.",
            actorName: request.OperatorName,
            commandType: nameof(FinalizeSessionCommand),
            outcome: SessionEvent.OutcomeSuccess);
        await _eventRepository.AddAsync(auditEvent, cancellationToken);
        await _eventRepository.SaveChangesAsync(cancellationToken);

        await _notifier.NotifyStateChangedAsync(session.Id, session.Status.ToString(), cancellationToken);

        return Result.Success(true);
    }
}
