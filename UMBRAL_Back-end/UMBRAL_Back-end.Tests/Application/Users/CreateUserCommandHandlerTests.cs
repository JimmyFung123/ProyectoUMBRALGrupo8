namespace UMBRAL_Back_end.Tests.Application.Users;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UserService.Application.Users;
using UserService.Application.Users.Commands.CreateUser;
using UserService.Domain.Common;
using UserService.Domain.Users;
using Xunit;

/// <summary>
/// HU-23 Criterio 1 + Flujo alterno: registro de usuarios con email único.
/// El admin no elige la contraseña: el sistema la genera, la guarda en
/// Keycloak como temporal y se la envía al usuario por correo.
/// </summary>
public class CreateUserCommandHandlerTests
{
    private const string GeneratedPassword = "Gen3r4ted#Pass";

    private readonly Mock<IKeycloakAdminClient> _keycloak = new();
    private readonly Mock<IPasswordGenerator> _passwordGenerator = new();
    private readonly Mock<IUserEmailSender> _emailSender = new();
    private readonly CreateUserCommandHandler _handler;

    public CreateUserCommandHandlerTests()
    {
        _passwordGenerator.Setup(g => g.Generate()).Returns(GeneratedPassword);
        _handler = new CreateUserCommandHandler(
            _keycloak.Object,
            _passwordGenerator.Object,
            _emailSender.Object,
            NullLogger<CreateUserCommandHandler>.Instance);
    }

    private CreateUserCommand ValidCommand(string? email = null) => new(
        Email: email ?? "nuevo@umbral.local",
        FirstName: "Nombre",
        LastName: "Apellido",
        Role: UserRole.Operator);

    private void SetupNoExistingUser() =>
        _keycloak.Setup(k => k.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((KeycloakUser?)null);

    private void SetupCreateSucceeds(Guid id) =>
        _keycloak.Setup(k => k.CreateUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(id));

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
        // Pre-check passes but Keycloak returns conflict — race condition.
        SetupNoExistingUser();
        _keycloak.Setup(k => k.CreateUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Guid>(UserErrors.EmailAlreadyInUse));

        var result = await _handler.Handle(ValidCommand(), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.EmailAlreadyInUse);
        _emailSender.Verify(e => e.SendTemporaryPasswordAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenValid_CreatesUserAndReturnsId()
    {
        var newId = Guid.NewGuid();
        SetupNoExistingUser();
        SetupCreateSucceeds(newId);

        var result = await _handler.Handle(ValidCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(newId);
    }

    [Fact]
    public async Task Handle_WhenValid_UsesGeneratedPasswordAndEmailsIt()
    {
        SetupNoExistingUser();
        SetupCreateSucceeds(Guid.NewGuid());

        await _handler.Handle(ValidCommand(), default);

        // La clave generada por el sistema es la que se guarda en Keycloak…
        _keycloak.Verify(k => k.CreateUserAsync(
            "nuevo@umbral.local", "Nombre", "Apellido",
            GeneratedPassword, UserRole.Operator, It.IsAny<CancellationToken>()),
            Times.Once);

        // …y la misma que se le envía al usuario por correo.
        _emailSender.Verify(e => e.SendTemporaryPasswordAsync(
            "nuevo@umbral.local", "Nombre", GeneratedPassword, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEmailDeliveryFails_StillReturnsSuccess()
    {
        // El usuario ya quedó creado: un fallo de correo no debe revertir ni
        // marcar la operación como fallida (sería confuso y dejaría el email
        // "ocupado"). Se loguea y se sigue.
        var newId = Guid.NewGuid();
        SetupNoExistingUser();
        SetupCreateSucceeds(newId);
        _emailSender.Setup(e => e.SendTemporaryPasswordAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        var result = await _handler.Handle(ValidCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(newId);
    }

    [Fact]
    public async Task Handle_TrimsAndLowercasesEmail()
    {
        SetupNoExistingUser();
        SetupCreateSucceeds(Guid.NewGuid());

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
