namespace UserService.Application.Users.Queries.GetUsers;

using MediatR;
using UserService.Domain.Users;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, IReadOnlyList<UserDto>>
{
    private readonly IKeycloakAdminClient _keycloak;

    public GetUsersQueryHandler(IKeycloakAdminClient keycloak) => _keycloak = keycloak;

    public async Task<IReadOnlyList<UserDto>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        var users = await _keycloak.ListUsersAsync(cancellationToken);

        return users
            // Hide the auto-created service-account user — it's an infra
            // detail, not real personnel.
            .Where(u => !u.Email.StartsWith("service-account-", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(u => u.Role == UserRole.Admin)
            .ThenBy(u => u.LastName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(u => u.FirstName, StringComparer.CurrentCultureIgnoreCase)
            .Select(u => new UserDto(
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Role?.ToKeycloakName(),
                u.Enabled))
            .ToList();
    }
}
