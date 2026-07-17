namespace SessionService.Application.Sessions.Commands.CreateSession;

using MediatR;
using SessionService.Application;
using SessionService.Domain.Common;
using SessionService.Domain.MissionLookup;
using SessionService.Domain.Sessions;
using UMBRAL.Contracts.Events;

public class CreateSessionCommandHandler : IRequestHandler<CreateSessionCommand, Result<Guid>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IMissionLookupRepository _missionLookupRepository;
    private readonly IIntegrationEventBus _bus;

    public CreateSessionCommandHandler(
        ISessionRepository sessionRepository,
        IMissionLookupRepository missionLookupRepository,
        IIntegrationEventBus bus)
    {
        _sessionRepository = sessionRepository;
        _missionLookupRepository = missionLookupRepository;
        _bus = bus;
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

        var sessionResult = Session.Create(request.MissionId, request.Name, scheduledAtUtc, request.OperatorId);
        if (sessionResult.IsFailure)
            return Result.Failure<Guid>(sessionResult.Error);

        var session = sessionResult.Value;
        await _sessionRepository.AddAsync(session, cancellationToken);
        await _sessionRepository.SaveChangesAsync(cancellationToken);

        // HU-26: command audit log entry — the first thing recorded for a session.
        await _bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                session.Id,
                $"Se creó la sesión '{session.Name}'.",
                ActorName: request.OperatorName,
                CommandType: nameof(CreateSessionCommand),
                Outcome: SessionEvent.OutcomeSuccess,
                DateTime.UtcNow),
            cancellationToken);

        return Result.Success(session.Id);
    }
}
