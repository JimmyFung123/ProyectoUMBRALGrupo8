// ApiGateway generates its own top-level-statements `Program` class in the *global*
// namespace, same conflict as the other five services (see the .csproj comment on the
// ApiGateway ProjectReference). Its ProjectReference carries Aliases=ApiGatewayAssembly
// metadata so this file resolves its Program via that extern alias instead of the bare
// (Mission's) `Program`.
extern alias ApiGatewayAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ApiGatewayProgram = ApiGatewayAssembly::Program;

/// <summary>
/// WebApplicationFactory for ApiGateway. Unlike every other service's factory, this one
/// does NOT derive from <c>IntegrationTestApiFactory&lt;TProgram, TDbContext&gt;</c> — the
/// gateway is a pure YARP reverse proxy with no database and no MassTransit bus to swap.
///
/// Two things get overridden via <see cref="IWebHostBuilder.UseSetting"/>-style config,
/// same technique as <c>IntegrationTestApiFactory</c> uses for the DB connection string:
///   * the mission-cluster destination address, redirected to a <see cref="Gateway.DownstreamStub"/>
///     instead of the real MissionService at localhost:5091, and
///   * (optionally) real Keycloak JwtBearer auth swapped for <see cref="TestAuthHandler"/> —
///     the same override SessionServiceApiFactory uses — so authenticated-path tests don't
///     need a live Keycloak token. Left off by default so the 401-without-token test exercises
///     the real auth pipeline end to end.
/// </summary>
public class ApiGatewayApiFactory(string missionClusterAddress, bool bypassAuth = false)
    : WebApplicationFactory<ApiGatewayProgram>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ReverseProxy:Clusters:mission-cluster:Destinations:destination1:Address",
            missionClusterAddress);

        if (bypassAuth)
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });
        }
    }
}
