namespace SessionService.Application.Sessions.Queries.GetSessionDetail;

using MediatR;
using SessionService.Domain.Common;

public record GetSessionDetailQuery(Guid SessionId) : IRequest<Result<SessionDetailDto>>;
