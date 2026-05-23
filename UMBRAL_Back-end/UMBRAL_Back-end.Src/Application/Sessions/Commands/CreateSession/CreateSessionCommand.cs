namespace UMBRAL_Back_end.Application.Sessions.Commands.CreateSession;

using MediatR;
using UMBRAL_Back_end.Domain.Common;

public record CreateSessionCommand(
    Guid MissionId,
    string Name,
    DateTime? ScheduledAt) : IRequest<Result<Guid>>;
