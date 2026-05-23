namespace UMBRAL_Back_end.Application.Sessions.Queries.GetSessionById;

using MediatR;
using UMBRAL_Back_end.Domain.Common;

public record GetSessionByIdQuery(Guid Id) : IRequest<Result<SessionDetailDto>>;
