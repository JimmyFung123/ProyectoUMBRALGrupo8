// SessionService generates its own top-level-statements `Program` class in the *global*
// namespace, same conflict as TeamService/ClueService/StageService (see the .csproj comment
// on the SessionService ProjectReference). Its ProjectReference carries
// Aliases=SessionServiceAssembly metadata so this file resolves its Program via that extern
// alias instead of the bare (Mission's) `Program`.
extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SessionServiceAssembly::SessionService.Infrastructure.Persistence;
using SessionProgram = SessionServiceAssembly::Program;

public class SessionServiceApiFactory(string connectionString)
    : IntegrationTestApiFactory<SessionProgram, SessionsDbContext>(connectionString)
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // SessionsController is the only Level-1-tested controller gated by [Authorize]
        // (HU-23 Keycloak JWT). Re-registering AddAuthentication here overrides the
        // DefaultScheme configured by Program.cs (last AddAuthentication call wins for
        // scalar AuthenticationOptions, same technique the ASP.NET Core docs recommend
        // for WebApplicationFactory auth overrides) so every request authenticates via
        // TestAuthHandler instead of requiring a real Keycloak-issued bearer token.
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
