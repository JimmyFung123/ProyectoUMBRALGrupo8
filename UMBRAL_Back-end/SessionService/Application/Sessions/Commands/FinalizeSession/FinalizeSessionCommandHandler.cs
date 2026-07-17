namespace SessionService.Application.Sessions.Commands.FinalizeSession;

using MediatR;
using SessionService.Application;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using SessionService.Domain.Statistics;
using UMBRAL.Contracts.Events;

public class FinalizeSessionCommandHandler : IRequestHandler<FinalizeSessionCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IIntegrationEventBus _bus;
    private readonly IStageCompletionRecordRepository _statsRepository;

    public FinalizeSessionCommandHandler(
        ISessionRepository sessionRepository,
        IIntegrationEventBus bus,
        IStageCompletionRecordRepository statsRepository)
    {
        _sessionRepository = sessionRepository;
        _bus = bus;
        _statsRepository = statsRepository;
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
        await _bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                request.SessionId,
                "La sesión fue finalizada. Ranking definitivo calculado.",
                ActorName: request.OperatorName,
                CommandType: nameof(FinalizeSessionCommand),
                Outcome: SessionEvent.OutcomeSuccess,
                DateTime.UtcNow),
            cancellationToken);

        await _bus.PublishAsync(
            new SessionStateChangedIntegrationEvent(session.Id, session.Status.ToString()),
            cancellationToken);

        return Result.Success(true);
    }
}
