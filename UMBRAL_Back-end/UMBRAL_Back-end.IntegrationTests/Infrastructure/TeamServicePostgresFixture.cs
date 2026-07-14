extern alias TeamServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using TeamServiceAssembly::TeamService.Infrastructure.Persistence;
using TeamProgram = TeamServiceAssembly::Program;
using Xunit;

public class TeamServicePostgresFixture : PostgresContainerFixture<TeamServiceApiFactory, TeamProgram, TeamsDbContext>
{
    protected override string DatabaseName => "teamservice_it";

    protected override TeamServiceApiFactory CreateFactory(string connectionString) => new(connectionString);
}

[CollectionDefinition(Name)]
public class TeamServiceCollection : ICollectionFixture<TeamServicePostgresFixture>
{
    public const string Name = "TeamService integration tests";
}
