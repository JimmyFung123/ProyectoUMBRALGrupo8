namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

/// <summary>
/// Generic <see cref="IAsyncLifetime"/> fixture shared by every service's Level-1
/// integration tests. Starts a real <c>postgres:15-alpine</c> container via
/// Testcontainers, builds the service's <typeparamref name="TFactory"/> against it,
/// and applies EF Core migrations for <typeparamref name="TDbContext"/> before any
/// test runs. Container/factory disposal always happens even if migration fails.
///
/// A subclass only needs to supply the database name and how to build
/// <typeparamref name="TFactory"/> from the container's connection string.
/// </summary>
public abstract class PostgresContainerFixture<TFactory, TProgram, TDbContext> : IAsyncLifetime
    where TFactory : WebApplicationFactory<TProgram>
    where TProgram : class
    where TDbContext : DbContext
{
    private PostgreSqlContainer _container = null!;

    public TFactory Factory { get; private set; } = null!;

    /// <summary>Isolated database name for this service's container (e.g. "teamservice_it").</summary>
    protected abstract string DatabaseName { get; }

    /// <summary>Builds the service-specific <see cref="WebApplicationFactory{TEntryPoint}"/> against the container's connection string.</summary>
    protected abstract TFactory CreateFactory(string connectionString);

    public async Task InitializeAsync()
    {
        // Built here (not in the constructor) so the abstract DatabaseName override
        // runs after the derived instance is fully constructed.
        _container = new PostgreSqlBuilder("postgres:15-alpine")
            .WithDatabase(DatabaseName)
            .WithUsername("umbral")
            .WithPassword("umbral")
            .Build();

        await _container.StartAsync();

        Factory = CreateFactory(_container.GetConnectionString());

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            await Factory.DisposeAsync();
        }
        finally
        {
            await _container.DisposeAsync();
        }
    }
}
