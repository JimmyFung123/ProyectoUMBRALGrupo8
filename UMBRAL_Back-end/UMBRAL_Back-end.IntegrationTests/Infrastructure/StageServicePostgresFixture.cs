extern alias StageServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using StageServiceAssembly::StageService.Infrastructure.Persistence;
using StageProgram = StageServiceAssembly::Program;
using Xunit;

public class StageServicePostgresFixture : PostgresContainerFixture<StageServiceApiFactory, StageProgram, StagesDbContext>
{
    protected override string DatabaseName => "stageservice_it";

    protected override StageServiceApiFactory CreateFactory(string connectionString) => new(connectionString);
}

[CollectionDefinition(Name)]
public class StageServiceCollection : ICollectionFixture<StageServicePostgresFixture>
{
    public const string Name = "StageService integration tests";
}
