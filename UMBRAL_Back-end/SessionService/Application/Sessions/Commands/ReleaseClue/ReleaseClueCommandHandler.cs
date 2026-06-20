namespace SessionService.Application.Sessions.Commands.ReleaseClue;

using MediatR;
using SessionService.Application;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using UMBRAL.Contracts.Events;

public class ReleaseClueCommandHandler : IRequestHandler<ReleaseClueCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamServiceClient;
    private readonly IIntegrationEventBus _bus;
    private readonly ISessionNotifier _notifier;

    public ReleaseClueCommandHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamServiceClient,
        IIntegrationEventBus bus,
        ISessionNotifier notifier)
    {
        _sessionRepository = sessionRepository;
        _teamServiceClient = teamServiceClient;
        _bus = bus;
        _notifier = notifier;
    }

    public async Task<Result<bool>> Handle(ReleaseClueCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<bool>(SessionErrors.NotFound);

        if (session.Status != SessionStatus.InProgress)
            return Result.Failure<bool>(SessionErrors.CannotReleaseClue);

        var cluesReceived = await _teamServiceClient.ReleaseClueAsync(
            request.TeamId,
            request.TotalCluesForStage,
            cancellationToken,
            isAutomatic: false);

        if (cluesReceived < 0)
            return Result.Failure<bool>(SessionErrors.AllCluesAlreadyReleased);

        // Audit log — resolve team name for human-readable message
        var teamInfo = await _teamServiceClient.GetTeamByIdAsync(request.TeamId, cancellationToken);
        var teamName = teamInfo?.Name ?? request.TeamId.ToString();

        var auditMessage = request.ClueContent is not null
            ? $"Pista #{cluesReceived} liberada al equipo '{teamName}': \"{request.ClueContent}\"."
            : $"Pista #{cluesReceived} liberada al equipo '{teamName}': zona geográfica (radio {request.ClueRadiusMeters ?? 0}m).";

        await _bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                request.SessionId,
                auditMessage,
                ActorName: request.OperatorName,
                CommandType: nameof(ReleaseClueCommand),
                Outcome: SessionEvent.OutcomeSuccess,
                DateTime.UtcNow),
            cancellationToken);

        await _notifier.NotifyClueReleasedAsync(
            request.SessionId, request.TeamId,
            request.ClueContent, request.ClueLatitude, request.ClueLongitude, request.ClueRadiusMeters,
            cluesReceived, isAutomatic: false,
            cancellationToken);

        return Result.Success(true);
    }
}
