namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using UMBRAL_Back_end.Infrastructure.Persistence;
using Xunit;

public class MissionServicePostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:15-alpine")
        .WithDatabase("missionservice_it")
        .WithUsername("umbral")
        .WithPassword("umbral")
        .Build();

    public MissionServiceApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        Factory = new MissionServiceApiFactory(_container.GetConnectionString());

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
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

[CollectionDefinition(Name)]
public class MissionServiceCollection : ICollectionFixture<MissionServicePostgresFixture>
{
    public const string Name = "MissionService integration tests";
}
