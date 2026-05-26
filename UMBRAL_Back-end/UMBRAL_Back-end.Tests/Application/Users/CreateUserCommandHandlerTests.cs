namespace UMBRAL_Back_end.Tests.Application.Users;

using FluentAssertions;
using Moq;
using UserService.Application.Users;
using UserService.Application.Users.Commands.CreateUser;
using UserService.Domain.Users;
using Xunit;

/// <summary>
/// HU-23 Criterio 1 + Flujo alterno: registro de usuarios con email único.
/// </summary>
public class CreateUserCommandHandlerTests
{
    private readonly Mock<IKeycloakAdminClient> _keycloak = new();
    private readonly CreateUserCommandHandler _handler;

    public CreateUserCommandHandlerTests()
    {
        _handler = new CreateUserCommandHandler(_keycloak.Object);
    }

    private CreateUserCommand ValidCommand(string? email = null) => new(
        Email: email ?? "nuevo@umbral.local",
        FirstName: "Nombre",
        LastName: "Apellido",
        TemporaryPassword: "Password123",
        Role: UserRole.Operator);

    // ── Validaciones de entrada ─────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sin-arroba")]
    [InlineData("@sin-local")]
    [InlineData("sin-dominio@")]
    public async Task Handle_WhenEmailInvalid_ReturnsInvalidEmail(string email)
    {
        var result = await _handler.Handle(ValidCommand(email), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidEmail);
        _keycloak.Verify(k => k.CreateUserAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenFirstNameBlank_ReturnsInvalidName()
    {
        var cmd = ValidCommand() with { FirstName = "  " };
        var result = await _handler.Handle(cmd, default);

        result.Error.Should().Be(UserErrors.InvalidName);
    }

    [Fact]
    public async Task Handle_WhenLastNameBlank_ReturnsInvalidName()
    {
        var cmd = ValidCommand() with { LastName = "" };
        var result = await _handler.Handle(cmd, default);

        result.Error.Should().Be(UserErrors.InvalidName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")] // 7 chars, below minimum
    public async Task Handle_WhenPasswordTooShort_ReturnsInvalidPassword(string password)
    {
        var cmd = ValidCommand() with { TemporaryPassword = password };
        var result = await _handler.Handle(cmd, default);

        result.Error.Should().Be(UserErrors.InvalidPassword);
    }

    // ── HU-23 Criterio 1 / Flujo alterno: email duplicado ───────────────────

    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ReturnsEmailAlreadyInUse()
    {
        var existing = new KeycloakUser(Guid.NewGuid(), "nuevo@umbral.local",
            "Existing", "User", true, UserRole.Operator);
        _keycloak.Setup(k => k.FindByEmailAsync("nuevo@umbral.local", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing);

        var result = await _handler.Handle(ValidCommand(), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.EmailAlreadyInUse);
        _keycloak.Verify(k => k.CreateUserAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenKeycloakRacesOnUniqueEmail_ReturnsEmailAlreadyInUse()
    {
        // Pre-check passes but Keycloak rejects with Conflict — race condition.
        _keycloak.Setup(k => k.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((KeycloakUser?)null);
        _keycloak.Setup(k => k.CreateUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeycloakConflictException("dup"));

        var result = await _handler.Handle(ValidCommand(), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.EmailAlreadyInUse);
    }

    // ── Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenValid_CreatesUserAndReturnsId()
    {
        var newId = Guid.NewGuid();
        _keycloak.Setup(k => k.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((KeycloakUser?)null);
        _keycloak.Setup(k => k.CreateUserAsync(
                "nuevo@umbral.local", "Nombre", "Apellido",
                "Password123", UserRole.Operator, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newId);

        var result = await _handler.Handle(ValidCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(newId);
    }

    [Fact]
    public async Task Handle_TrimsAndLowercasesEmail()
    {
        _keycloak.Setup(k => k.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((KeycloakUser?)null);
        _keycloak.Setup(k => k.CreateUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var cmd = ValidCommand() with { Email = "  USUARIO@Umbral.LOCAL  " };
        await _handler.Handle(cmd, default);

        _keycloak.Verify(k => k.FindByEmailAsync("usuario@umbral.local", It.IsAny<CancellationToken>()), Times.Once);
        _keycloak.Verify(k => k.CreateUserAsync(
            "usuario@umbral.local",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<UserRole>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
