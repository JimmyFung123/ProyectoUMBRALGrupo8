namespace UMBRAL_Back_end.Tests.Application.Users;

using FluentAssertions;
using Moq;
using UserService.Application.Users;
using UserService.Application.Users.Commands.DisableUser;
using UserService.Domain.Users;
using Xunit;

/// <summary>
/// HU-23 Criterio 3: deshabilitar = enabled=false (nunca borrar).
/// HU-23 Criterio 4: no auto-desactivarse + no deshabilitar al único admin.
/// </summary>
public class DisableUserCommandHandlerTests
{
    private readonly Mock<IKeycloakAdminClient> _keycloak = new();
    private readonly DisableUserCommandHandler _handler;

    public DisableUserCommandHandlerTests()
    {
        _handler = new DisableUserCommandHandler(_keycloak.Object);
    }

    private static KeycloakUser MakeUser(
        Guid id, UserRole? role, bool enabled = true, string email = "u@umbral.local") =>
        new(id, email, "First", "Last", enabled, role);

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _keycloak.Setup(k => k.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((KeycloakUser?)null);

        var result = await _handler.Handle(new DisableUserCommand(id, null), default);

        result.Error.Should().Be(UserErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenAlreadyDisabled_IsIdempotentSuccess()
    {
        var id = Guid.NewGuid();
        _keycloak.Setup(k => k.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(MakeUser(id, UserRole.Operator, enabled: false));

        var result = await _handler.Handle(new DisableUserCommand(id, null), default);

        result.IsSuccess.Should().BeTrue();
        _keycloak.Verify(k => k.DisableAsync(id, It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── HU-23 Criterio 4a: no auto-desactivarse ─────────────────────────────

    [Fact]
    public async Task Handle_WhenRequesterEmailMatchesTargetEmail_ReturnsCannotDisableSelf()
    {
        var id = Guid.NewGuid();
        _keycloak.Setup(k => k.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(MakeUser(id, UserRole.Admin, email: "admin@umbral.local"));

        var result = await _handler.Handle(new DisableUserCommand(id, "admin@umbral.local"), default);

        result.Error.Should().Be(UserErrors.CannotDisableSelf);
    }

    [Fact]
    public async Task Handle_WhenRequesterEmailCaseDiffers_StillReturnsCannotDisableSelf()
    {
        // Defensa: el email debe compararse case-insensitive.
        var id = Guid.NewGuid();
        _keycloak.Setup(k => k.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(MakeUser(id, UserRole.Admin, email: "admin@umbral.local"));

        var result = await _handler.Handle(new DisableUserCommand(id, "ADMIN@UMBRAL.LOCAL"), default);

        result.Error.Should().Be(UserErrors.CannotDisableSelf);
    }

    // ── HU-23 Criterio 4b: no deshabilitar al último admin ──────────────────

    [Fact]
    public async Task Handle_WhenDisablingTheOnlyActiveAdmin_ReturnsCannotDisableLastAdmin()
    {
        var adminId = Guid.NewGuid();
        var admin = MakeUser(adminId, UserRole.Admin);
        var op    = MakeUser(Guid.NewGuid(), UserRole.Operator);

        _keycloak.Setup(k => k.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(admin);
        _keycloak.Setup(k => k.ListUsersAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[] { admin, op });

        // Requesting user is someone else — so the "self" check passes.
        var result = await _handler.Handle(
            new DisableUserCommand(adminId, "otro-admin@umbral.local"), default);

        result.Error.Should().Be(UserErrors.CannotDisableLastAdmin);
        _keycloak.Verify(k => k.DisableAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDisablingOneOfManyAdmins_Succeeds()
    {
        var adminId = Guid.NewGuid();
        var admin   = MakeUser(adminId, UserRole.Admin, email: "a@umbral.local");
        var admin2  = MakeUser(Guid.NewGuid(), UserRole.Admin, email: "b@umbral.local");

        _keycloak.Setup(k => k.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(admin);
        _keycloak.Setup(k => k.ListUsersAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[] { admin, admin2 });

        var result = await _handler.Handle(
            new DisableUserCommand(adminId, "b@umbral.local"), default);

        result.IsSuccess.Should().BeTrue();
        _keycloak.Verify(k => k.DisableAsync(adminId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDisablingOperator_DoesNotCheckAdminCount()
    {
        // Solo se chequea cantidad de admins si el usuario es admin.
        var opId = Guid.NewGuid();
        _keycloak.Setup(k => k.GetByIdAsync(opId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(MakeUser(opId, UserRole.Operator));

        var result = await _handler.Handle(new DisableUserCommand(opId, "admin@umbral.local"), default);

        result.IsSuccess.Should().BeTrue();
        _keycloak.Verify(k => k.ListUsersAsync(It.IsAny<CancellationToken>()), Times.Never);
        _keycloak.Verify(k => k.DisableAsync(opId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
