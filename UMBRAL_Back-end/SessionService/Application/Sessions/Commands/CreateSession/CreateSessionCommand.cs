namespace SessionService.Application.Sessions.Commands.CreateSession;

using MediatR;
using SessionService.Domain.Common;

public record CreateSessionCommand(
    Guid MissionId,
    string Name,
    DateTime? ScheduledAt,
    string? OperatorName = null) : IRequest<Result<Guid>>;
