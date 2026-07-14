extern alias ClueServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using ClueServiceAssembly::ClueService.Infrastructure.Persistence;
using ClueProgram = ClueServiceAssembly::Program;
using Xunit;

public class ClueServicePostgresFixture : PostgresContainerFixture<ClueServiceApiFactory, ClueProgram, CluesDbContext>
{
    protected override string DatabaseName => "clueservice_it";

    protected override ClueServiceApiFactory CreateFactory(string connectionString) => new(connectionString);
}

[CollectionDefinition(Name)]
public class ClueServiceCollection : ICollectionFixture<ClueServicePostgresFixture>
{
    public const string Name = "ClueService integration tests";
}
