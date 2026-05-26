namespace UMBRAL_Back_end.Tests.Application.Users;

using FluentAssertions;
using Moq;
using UserService.Application.Users;
using UserService.Application.Users.Commands.EnableUser;
using UserService.Domain.Users;
using Xunit;

public class EnableUserCommandHandlerTests
{
    private readonly Mock<IKeycloakAdminClient> _keycloak = new();
    private readonly EnableUserCommandHandler _handler;

    public EnableUserCommandHandlerTests()
    {
        _handler = new EnableUserCommandHandler(_keycloak.Object);
    }

    private static KeycloakUser MakeUser(Guid id, bool enabled) =>
        new(id, "u@umbral.local", "First", "Last", enabled, UserRole.Operator);

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _keycloak.Setup(k => k.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((KeycloakUser?)null);

        var result = await _handler.Handle(new EnableUserCommand(id), default);

        result.Error.Should().Be(UserErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenAlreadyEnabled_IsIdempotentSuccess()
    {
        var id = Guid.NewGuid();
        _keycloak.Setup(k => k.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(MakeUser(id, enabled: true));

        var result = await _handler.Handle(new EnableUserCommand(id), default);

        result.IsSuccess.Should().BeTrue();
        _keycloak.Verify(k => k.EnableAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDisabled_EnablesUser()
    {
        var id = Guid.NewGuid();
        _keycloak.Setup(k => k.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(MakeUser(id, enabled: false));

        var result = await _handler.Handle(new EnableUserCommand(id), default);

        result.IsSuccess.Should().BeTrue();
        _keycloak.Verify(k => k.EnableAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
