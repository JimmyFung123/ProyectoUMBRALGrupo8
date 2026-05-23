namespace UMBRAL_Back_end.Application.Sessions.Commands.CreateSession;

using MediatR;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Domain.Sessions;
using UMBRAL_Back_end.Domain.Sessions.Events;

public class CreateSessionCommandHandler : IRequestHandler<CreateSessionCommand, Result<Guid>>
{
    private readonly IMissionRepository _missionRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IPublisher _publisher;

    public CreateSessionCommandHandler(
        IMissionRepository missionRepository,
        ISessionRepository sessionRepository,
        IPublisher publisher)
    {
        _missionRepository = missionRepository;
        _sessionRepository = sessionRepository;
        _publisher = publisher;
    }

    public async Task<Result<Guid>> Handle(CreateSessionCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            return Result.Failure<Guid>(MissionErrors.NotFound);

        if (mission.Status != MissionStatus.Active)
            return Result.Failure<Guid>(SessionErrors.MissionNotActive);

        // Normalize ScheduledAt to UTC — datetime-local inputs arrive as Kind=Unspecified
        // which PostgreSQL's timestamptz rejects. Treat them as UTC.
        var scheduledAtUtc = request.ScheduledAt.HasValue
            ? DateTime.SpecifyKind(request.ScheduledAt.Value, DateTimeKind.Utc)
            : (DateTime?)null;

        var sessionResult = Session.Create(request.MissionId, request.Name, scheduledAtUtc);
        if (sessionResult.IsFailure)
            return Result.Failure<Guid>(sessionResult.Error);

        var session = sessionResult.Value;

        await _sessionRepository.AddAsync(session, cancellationToken);
        await _sessionRepository.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(
            new SessionCreatedEvent(session.Id, session.MissionId, session.Name, session.CreatedAt),
            cancellationToken);

        return Result.Success(session.Id);
    }
}
