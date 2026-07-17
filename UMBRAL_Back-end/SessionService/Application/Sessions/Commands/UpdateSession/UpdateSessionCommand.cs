namespace SessionService.Application.Sessions.Commands.UpdateSession;

using MediatR;
using SessionService.Domain.Common;

public record UpdateSessionCommand(
    Guid SessionId,
    string Name,
    DateTime? ScheduledAt,
    string? OperatorName = null) : IRequest<Result<bool>>;
