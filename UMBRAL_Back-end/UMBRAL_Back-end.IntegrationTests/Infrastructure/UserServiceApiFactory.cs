// UserService generates its own top-level-statements `Program` class in the *global*
// namespace, same conflict as the other services (see the .csproj comment on the
// UserService ProjectReference). Its ProjectReference carries Aliases=UserServiceAssembly
// metadata so this file resolves its Program via that extern alias instead of the bare
// (Mission's) `Program`.
extern alias UserServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UserServiceAssembly::UserService.Application.Users;
using UserProgram = UserServiceAssembly::Program;

/// <summary>
/// WebApplicationFactory for UserService (HU-23). Unlike every other Level-1-tested
/// service, UserService has no database and no MassTransit bus to swap — it's a thin
/// CQRS layer over Keycloak's Admin REST API. Like ApiGatewayApiFactory, it does NOT
/// derive from <c>IntegrationTestApiFactory&lt;TProgram, TDbContext&gt;</c> (there is
/// no <c>TDbContext</c>).
///
/// Three things get overridden here, all via <see cref="IWebHostBuilder.UseSetting"/>
/// / <see cref="IWebHostBuilder.ConfigureServices"/>, same technique the other
/// factories use:
///   * <c>Keycloak:*</c> settings, pointed at the real Testcontainers Keycloak
///     instance (see <see cref="UserServiceKeycloakFixture"/>) instead of the dev
///     docker-compose one at localhost:18090.
///   * <see cref="IUserEmailSender"/> swapped for <see cref="NoOpUserEmailSender"/> —
///     email delivery is out of scope for this suite (see its doc comment).
///   * JwtBearer auth swapped for <see cref="AdminTestAuthHandler"/> — UsersController
///     requires <c>[Authorize(Roles = "admin")]</c>, and the shared
///     <see cref="TestAuthHandler"/> only carries an "operator" role, so reusing it
///     here would 403 every request (see AdminTestAuthHandler's doc comment).
/// </summary>
public class UserServiceApiFactory(
    string keycloakAdminBaseUrl,
    string keycloakRealm,
    string keycloakAdminClientId,
    string keycloakAdminClientSecret)
    : WebApplicationFactory<UserProgram>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Keycloak:AdminBaseUrl", keycloakAdminBaseUrl);
        builder.UseSetting("Keycloak:Realm", keycloakRealm);
        builder.UseSetting("Keycloak:AdminClientId", keycloakAdminClientId);
        builder.UseSetting("Keycloak:AdminClientSecret", keycloakAdminClientSecret);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserEmailSender>();
            services.AddSingleton<IUserEmailSender, NoOpUserEmailSender>();

            services.AddAuthentication(AdminTestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, AdminTestAuthHandler>(
                    AdminTestAuthHandler.SchemeName, _ => { });
        });
    }
}
