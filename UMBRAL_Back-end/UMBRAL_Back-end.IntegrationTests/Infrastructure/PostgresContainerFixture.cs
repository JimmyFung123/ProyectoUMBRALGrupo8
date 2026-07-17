namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Respawn.Graph;
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
///
/// All tests in a collection share this same container/database (one instance per
/// collection, per xUnit's <see cref="ICollectionFixture{TFixture}"/> contract), so
/// call <see cref="ResetDatabaseAsync"/> before each individual test (e.g. from each
/// test class's own <see cref="IAsyncLifetime.InitializeAsync"/>) to avoid leaking
/// rows between tests.
/// </summary>
public abstract class PostgresContainerFixture<TFactory, TProgram, TDbContext> : IAsyncLifetime
    where TFactory : WebApplicationFactory<TProgram>
    where TProgram : class
    where TDbContext : DbContext
{
    private PostgreSqlContainer _container = null!;
    private Respawner _respawner = null!;

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

        // Snapshot the schema once (post-migration) so ResetDatabaseAsync can truncate
        // all app tables between tests without re-discovering the schema every time.
        // __EFMigrationsHistory is excluded so the "no pending migrations" tests (which
        // read applied-migrations state) keep working after a reset.
        await using var respawnConnection = new NpgsqlConnection(_container.GetConnectionString());
        await respawnConnection.OpenAsync();
        _respawner = await Respawner.CreateAsync(respawnConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            TablesToIgnore = [new Table("__EFMigrationsHistory")]
        });
    }

    /// <summary>
    /// Truncates every app table in this fixture's database (except migrations history),
    /// so each test starts from a clean slate instead of seeing rows left over by earlier
    /// tests sharing the same collection-scoped container. Intended to be called from each
    /// test class's own <see cref="IAsyncLifetime.InitializeAsync"/>, which xUnit runs
    /// before every individual [Fact].
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
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
