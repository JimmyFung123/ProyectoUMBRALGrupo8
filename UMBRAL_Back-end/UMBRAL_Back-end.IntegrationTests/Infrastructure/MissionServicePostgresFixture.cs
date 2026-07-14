namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using UMBRAL_Back_end.Infrastructure.Persistence;
using Xunit;

public class MissionServicePostgresFixture : PostgresContainerFixture<MissionServiceApiFactory, Program, AppDbContext>
{
    protected override string DatabaseName => "missionservice_it";

    protected override MissionServiceApiFactory CreateFactory(string connectionString) => new(connectionString);
}

[CollectionDefinition(Name)]
public class MissionServiceCollection : ICollectionFixture<MissionServicePostgresFixture>
{
    public const string Name = "MissionService integration tests";
}
