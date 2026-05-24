namespace SessionService.Application.Sessions.Queries.GetSessionDashboard;

using MediatR;
using SessionService.Domain.Common;

public record GetSessionDashboardQuery(Guid SessionId) : IRequest<Result<SessionDashboardDto>>;
