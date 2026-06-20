namespace SessionService.Application.Sessions.Commands.UpdateSession;

using MediatR;
using SessionService.Application;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using UMBRAL.Contracts.Events;

public class UpdateSessionCommandHandler : IRequestHandler<UpdateSessionCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IIntegrationEventBus _bus;

    public UpdateSessionCommandHandler(
        ISessionRepository sessionRepository,
        IIntegrationEventBus bus)
    {
        _sessionRepository = sessionRepository;
        _bus = bus;
    }

    public async Task<Result<bool>> Handle(UpdateSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<bool>(SessionErrors.NotFound);

        // Normalize datetime: frontend sends datetime-local (Kind=Unspecified)
        var scheduledAtUtc = request.ScheduledAt.HasValue
            ? DateTime.SpecifyKind(request.ScheduledAt.Value, DateTimeKind.Utc)
            : (DateTime?)null;

        var updateResult = session.Update(request.Name, scheduledAtUtc);
        if (updateResult.IsFailure)
            return updateResult;

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        // HU-26: command audit log entry.
        await _bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                request.SessionId,
                $"Se editaron los datos de la sesión (nombre: '{session.Name}').",
                ActorName: request.OperatorName,
                CommandType: nameof(UpdateSessionCommand),
                Outcome: SessionEvent.OutcomeSuccess,
                DateTime.UtcNow),
            cancellationToken);

        return Result.Success(true);
    }
}
