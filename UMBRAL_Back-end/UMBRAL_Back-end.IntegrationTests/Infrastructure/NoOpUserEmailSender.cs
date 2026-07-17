// This file touches types that live in UserService's aliased assembly (see the
// .csproj comment on the UserService ProjectReference for why the alias exists).
extern alias UserServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using UserServiceAssembly::UserService.Application.Users;

/// <summary>
/// No-op stand-in for <see cref="IUserEmailSender"/> used by
/// <see cref="UserServiceApiFactory"/>. Sending the temporary-password email
/// requires a live SMTP server (Mailpit in dev, localhost:1025) — that pipeline
/// is explicitly out of scope for the Keycloak Admin API regression suite (see
/// UserServiceKeycloakFixture.cs); this fixture proves CreateUserAsync/ChangeRoleAsync
/// against a REAL Keycloak, not email delivery.
///
/// Note this isn't strictly required for correctness: CreateUserCommandHandler
/// already wraps the email send in try/catch and only logs on failure (it never
/// fails the command), so a real SmtpUserEmailSender hitting a closed
/// localhost:1025 would fail fast (connection refused) without blocking the test.
/// This fake is registered anyway to keep the test hermetic and avoid noisy
/// "email delivery failed" error logs on every run.
/// </summary>
public sealed class NoOpUserEmailSender : IUserEmailSender
{
    public Task SendTemporaryPasswordAsync(
        string email, string firstName, string temporaryPassword, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
