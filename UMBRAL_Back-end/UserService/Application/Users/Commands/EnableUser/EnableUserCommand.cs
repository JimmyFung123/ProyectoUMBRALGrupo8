namespace UserService.Application.Users.Commands.EnableUser;

using MediatR;
using UserService.Domain.Common;

public record EnableUserCommand(Guid UserId) : IRequest<Result>;
