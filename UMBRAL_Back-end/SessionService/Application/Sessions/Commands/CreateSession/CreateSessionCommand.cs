namespace SessionService.Application.Sessions.Commands.CreateSession;

using MediatR;
using SessionService.Domain.Common;

public record CreateSessionCommand(
    Guid MissionId,
    string Name,
    DateTime? ScheduledAt,
    string? OperatorName = null,
    string? OperatorId = null) : IRequest<Result<Guid>>;
