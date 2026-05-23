namespace SessionService.Application.Sessions.Commands.UpdateSession;

using MediatR;
using SessionService.Domain.Common;

public record UpdateSessionCommand(
    Guid SessionId,
    string Name,
    DateTime? ScheduledAt) : IRequest<Result<bool>>;
