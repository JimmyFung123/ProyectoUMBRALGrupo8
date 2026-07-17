namespace SessionService.Application.Sessions.Commands.CreateSession;

using MediatR;
using SessionService.Application;
using SessionService.Domain.Common;
using SessionService.Domain.MissionLookup;
using SessionService.Domain.Sessions;

public class CreateSessionCommandHandler : IRequestHandler<CreateSessionCommand, Result<Guid>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IMissionLookupRepository _missionLookupRepository;

    public CreateSessionCommandHandler(
        ISessionRepository sessionRepository,
        IMissionLookupRepository missionLookupRepository)
    {
        _sessionRepository = sessionRepository;
        _missionLookupRepository = missionLookupRepository;
    }

    public async Task<Result<Guid>> Handle(CreateSessionCommand request, CancellationToken cancellationToken)
    {
        // ── Database-per-Service: SessionService does NOT call MissionService's DB.
        //    Instead it reads from its own MissionLookup replica, kept in sync via
        //    integration events (RabbitMQ → MassTransit consumers).
        var missionLookup = await _missionLookupRepository.GetByIdAsync(request.MissionId, cancellationToken);
        if (missionLookup is null)
            return Result.Failure<Guid>(MissionLookupErrors.NotFound);

        if (!missionLookup.IsActive)
            return Result.Failure<Guid>(SessionErrors.MissionNotActive);

        // Normalize ScheduledAt: datetime-local inputs arrive as Kind=Unspecified
        var scheduledAtUtc = request.ScheduledAt.HasValue
            ? DateTime.SpecifyKind(request.ScheduledAt.Value, DateTimeKind.Utc)
            : (DateTime?)null;

        var sessionResult = Session.Create(
            request.MissionId, request.Name, scheduledAtUtc, request.OperatorId, request.OperatorName);
        if (sessionResult.IsFailure)
            return Result.Failure<Guid>(sessionResult.Error);

        var session = sessionResult.Value;
        await _sessionRepository.AddAsync(session, cancellationToken);
        await _sessionRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(session.Id);
    }
}
