namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Level-2 counterpart to <see cref="IntegrationTestApiFactory{TProgram,TDbContext}"/>.
///
/// Level-1 factories force "Messaging:UseInMemoryTransport" = true so tests never touch
/// a real broker. This factory does the opposite on purpose: it leaves that setting
/// UNSET (defaults to false via <c>IConfiguration.GetValue&lt;bool&gt;</c>), so
/// <c>UmbralMessagingExtensions.AddUmbralMassTransit</c> falls into its real
/// <c>UsingRabbitMq</c> branch — the same code path used in production — but with
/// "ConnectionStrings:RabbitMQ" overridden to point at a Testcontainers RabbitMQ
/// instance instead of the appsettings.json default (localhost:5672). This is what lets
/// Level-2 tests validate actual MassTransit routing/serialization/queue-prefix behavior
/// against a disposable, isolated broker.
/// </summary>
public class RabbitMqBackedApiFactory<TProgram, TDbContext>(string connectionString, string rabbitMqConnectionString)
    : WebApplicationFactory<TProgram>
    where TProgram : class
    where TDbContext : DbContext
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:RabbitMQ", rabbitMqConnectionString);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TDbContext>>();
            services.AddDbContext<TDbContext>(options => options.UseNpgsql(connectionString));
        });
    }
}
