namespace UMBRAL_Back_end.Tests.Application.Users;

using FluentAssertions;
using Moq;
using UserService.Application.Users;
using UserService.Application.Users.Queries.GetUsers;
using UserService.Domain.Users;
using Xunit;

public class GetUsersQueryHandlerTests
{
    private readonly Mock<IKeycloakAdminClient> _keycloak = new();
    private readonly GetUsersQueryHandler _handler;

    public GetUsersQueryHandlerTests()
    {
        _handler = new GetUsersQueryHandler(_keycloak.Object);
    }

    private static KeycloakUser User(string email, UserRole? role, bool enabled = true,
        string first = "First", string last = "Last") =>
        new(Guid.NewGuid(), email, first, last, enabled, role);

    [Fact]
    public async Task Handle_ReturnsAdminsFirstThenAlphabetical()
    {
        var users = new[]
        {
            User("zorro@umbral.local",   UserRole.Operator, first: "Z",   last: "Zorro"),
            User("admin@umbral.local",   UserRole.Admin,    first: "A",   last: "Admin"),
            User("aguila@umbral.local",  UserRole.Operator, first: "Ag",  last: "Aguila"),
        };
        _keycloak.Setup(k => k.ListUsersAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(users);

        var result = await _handler.Handle(new GetUsersQuery(), default);

        result.Should().HaveCount(3);
        result[0].Email.Should().Be("admin@umbral.local");         // admin primero
        result[1].Email.Should().Be("aguila@umbral.local");        // luego operadores alfabético
        result[2].Email.Should().Be("zorro@umbral.local");
    }

    [Fact]
    public async Task Handle_HidesServiceAccountUsers()
    {
        // El usuario service-account-umbral-backend NO debe verse en la UI.
        var users = new[]
        {
            User("admin@umbral.local", UserRole.Admin),
            User("service-account-umbral-backend", null,
                 first: "service-account-umbral-backend", last: ""),
        };
        _keycloak.Setup(k => k.ListUsersAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(users);

        var result = await _handler.Handle(new GetUsersQuery(), default);

        result.Should().HaveCount(1);
        result[0].Email.Should().Be("admin@umbral.local");
    }

    [Fact]
    public async Task Handle_MapsRoleToKeycloakLowercaseString()
    {
        var users = new[]
        {
            User("a@x.com",  UserRole.Admin),
            User("b@x.com",  UserRole.Operator),
            User("c@x.com",  null),
        };
        _keycloak.Setup(k => k.ListUsersAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(users);

        var result = await _handler.Handle(new GetUsersQuery(), default);

        result.Single(u => u.Email == "a@x.com").Role.Should().Be("admin");
        result.Single(u => u.Email == "b@x.com").Role.Should().Be("operator");
        result.Single(u => u.Email == "c@x.com").Role.Should().BeNull();
    }
}
