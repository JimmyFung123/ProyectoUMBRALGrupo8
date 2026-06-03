namespace SessionService.Application.Sessions.Commands.StartSession;

using MediatR;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

public class StartSessionCommandHandler : IRequestHandler<StartSessionCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamServiceClient;
    private readonly ISessionEventRepository _eventRepository;
    private readonly ISessionNotifier _notifier;

    public StartSessionCommandHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamServiceClient,
        ISessionEventRepository eventRepository,
        ISessionNotifier notifier)
    {
        _sessionRepository = sessionRepository;
        _teamServiceClient = teamServiceClient;
        _eventRepository = eventRepository;
        _notifier = notifier;
    }

    public async Task<Result<bool>> Handle(StartSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<bool>(SessionErrors.NotFound);

        // HU-12 / RB-02: at least one enrolled team is required to start
        var hasTeams = await _teamServiceClient.HasEnrolledTeamsAsync(request.SessionId, cancellationToken);
        if (!hasTeams)
            return Result.Failure<bool>(SessionErrors.NoTeamsEnrolled);

        // RB-18: every team must have at least 2 members
        var allMeetMinimum = await _teamServiceClient.AllTeamsMeetMinimumMembersAsync(request.SessionId, minMembers: 2, cancellationToken);
        if (!allMeetMinimum)
            return Result.Failure<bool>(SessionErrors.TeamBelowMinimumMembers);

        var result = session.Start();
        if (result.IsFailure)
            return result;

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        // HU-22 / HU-26: audit log of the state change
        var auditEvent = SessionEvent.Create(
            request.SessionId,
            "La sesión fue iniciada.",
            actorName: request.OperatorName,
            commandType: nameof(StartSessionCommand),
            outcome: SessionEvent.OutcomeSuccess);
        await _eventRepository.AddAsync(auditEvent, cancellationToken);
        await _eventRepository.SaveChangesAsync(cancellationToken);

        await _notifier.NotifyStateChangedAsync(session.Id, session.Status.ToString(), cancellationToken);

        return Result.Success(true);
    }
}
