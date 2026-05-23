namespace UMBRAL_Back_end.Application.Sessions.Queries.GetSessions;

using MediatR;

public record GetSessionsQuery(
    Guid? MissionId = null,
    string? Status = null) : IRequest<IReadOnlyList<SessionDto>>;
