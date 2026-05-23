namespace SessionService.Application.Sessions.Commands.UpdateSession;

using MediatR;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

public class UpdateSessionCommandHandler : IRequestHandler<UpdateSessionCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;

    public UpdateSessionCommandHandler(ISessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
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
        return Result.Success(true);
    }
}
