namespace UMBRAL_Back_end.Tests.Application.Users;

using FluentAssertions;
using Moq;
using UserService.Application.Users;
using UserService.Application.Users.Commands.ChangeRole;
using UserService.Domain.Users;
using Xunit;

/// <summary>
/// HU-23 Criterio 2 (cambio de rol) + Criterio 4 (protección al último admin).
/// </summary>
public class ChangeRoleCommandHandlerTests
{
    private readonly Mock<IKeycloakAdminClient> _keycloak = new();
    private readonly ChangeRoleCommandHandler _handler;

    public ChangeRoleCommandHandlerTests()
    {
        _handler = new ChangeRoleCommandHandler(_keycloak.Object);
    }

    private static KeycloakUser MakeUser(Guid id, UserRole? role, bool enabled = true) =>
        new(id, $"u_{id}@umbral.local", "First", "Last", enabled, role);

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _keycloak.Setup(k => k.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((KeycloakUser?)null);

        var result = await _handler.Handle(new ChangeRoleCommand(id, UserRole.Admin), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenRoleUnchanged_SucceedsWithoutCallingKeycloak()
    {
        var id = Guid.NewGuid();
        _keycloak.Setup(k => k.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(MakeUser(id, UserRole.Operator));

        var result = await _handler.Handle(new ChangeRoleCommand(id, UserRole.Operator), default);

        result.IsSuccess.Should().BeTrue();
        _keycloak.Verify(k => k.ChangeRoleAsync(
            It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── HU-23 Criterio 4: no degradar al único administrador ────────────────

    [Fact]
    public async Task Handle_WhenDemotingTheOnlyActiveAdmin_ReturnsCannotDemoteLastAdmin()
    {
        var adminId = Guid.NewGuid();
        var admin   = MakeUser(adminId, UserRole.Admin);
        var op      = MakeUser(Guid.NewGuid(), UserRole.Operator);

        _keycloak.Setup(k => k.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(admin);
        _keycloak.Setup(k => k.ListUsersAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[] { admin, op });

        var result = await _handler.Handle(new ChangeRoleCommand(adminId, UserRole.Operator), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.CannotDemoteLastAdmin);
        _keycloak.Verify(k => k.ChangeRoleAsync(
            It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDemotingOneOfManyAdmins_Succeeds()
    {
        var adminId = Guid.NewGuid();
        var admin   = MakeUser(adminId, UserRole.Admin);
        var admin2  = MakeUser(Guid.NewGuid(), UserRole.Admin);

        _keycloak.Setup(k => k.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(admin);
        _keycloak.Setup(k => k.ListUsersAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[] { admin, admin2 });

        var result = await _handler.Handle(new ChangeRoleCommand(adminId, UserRole.Operator), default);

        result.IsSuccess.Should().BeTrue();
        _keycloak.Verify(k => k.ChangeRoleAsync(adminId, UserRole.Operator, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDemotingButTheOtherAdminIsInactive_Fails()
    {
        // Si el segundo admin está deshabilitado, no cuenta como "activo".
        var adminId = Guid.NewGuid();
        var admin    = MakeUser(adminId, UserRole.Admin, enabled: true);
        var inactive = MakeUser(Guid.NewGuid(), UserRole.Admin, enabled: false);

        _keycloak.Setup(k => k.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(admin);
        _keycloak.Setup(k => k.ListUsersAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[] { admin, inactive });

        var result = await _handler.Handle(new ChangeRoleCommand(adminId, UserRole.Operator), default);

        result.Error.Should().Be(UserErrors.CannotDemoteLastAdmin);
    }

    // ── Promoción operator → admin: no requiere chequeo de último admin ─────

    [Fact]
    public async Task Handle_WhenPromotingOperatorToAdmin_Succeeds()
    {
        var id = Guid.NewGuid();
        _keycloak.Setup(k => k.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(MakeUser(id, UserRole.Operator));

        var result = await _handler.Handle(new ChangeRoleCommand(id, UserRole.Admin), default);

        result.IsSuccess.Should().BeTrue();
        _keycloak.Verify(k => k.ListUsersAsync(It.IsAny<CancellationToken>()), Times.Never);
        _keycloak.Verify(k => k.ChangeRoleAsync(id, UserRole.Admin, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
