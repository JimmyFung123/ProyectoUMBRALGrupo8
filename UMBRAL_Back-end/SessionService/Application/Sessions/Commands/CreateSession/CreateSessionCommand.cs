namespace SessionService.Application.Sessions.Commands.CreateSession;

using MediatR;
using SessionService.Domain.Common;

public record CreateSessionCommand(
    Guid MissionId,
    string Name,
    DateTime? ScheduledAt) : IRequest<Result<Guid>>;
