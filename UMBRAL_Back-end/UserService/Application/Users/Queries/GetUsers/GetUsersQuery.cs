namespace UserService.Application.Users.Queries.GetUsers;

using MediatR;

public record GetUsersQuery() : IRequest<IReadOnlyList<UserDto>>;
