namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Generic <see cref="WebApplicationFactory{TEntryPoint}"/> shared by every
/// service's Level-1 integration tests. Forces the MassTransit in-memory
/// transport (no RabbitMQ needed for tests) and swaps the service's real
/// <typeparamref name="TDbContext"/> registration for one pointed at the
/// Testcontainers Postgres instance passed in via <paramref name="connectionString"/>.
///
/// One thin subclass per service supplies <typeparamref name="TProgram"/> (the
/// service's top-level-statements <c>Program</c> class) and
/// <typeparamref name="TDbContext"/> — everything else is identical across services.
/// </summary>
public class IntegrationTestApiFactory<TProgram, TDbContext>(string connectionString)
    : WebApplicationFactory<TProgram>
    where TProgram : class
    where TDbContext : DbContext
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Messaging:UseInMemoryTransport", "true");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TDbContext>>();
            services.AddDbContext<TDbContext>(options => options.UseNpgsql(connectionString));
        });
    }
}
