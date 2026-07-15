extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using SessionServiceAssembly::SessionService.Infrastructure.Persistence;
using SessionProgram = SessionServiceAssembly::Program;
using Xunit;

public class SessionServicePostgresFixture : PostgresContainerFixture<SessionServiceApiFactory, SessionProgram, SessionsDbContext>
{
    protected override string DatabaseName => "sessionservice_it";

    protected override SessionServiceApiFactory CreateFactory(string connectionString) => new(connectionString);
}

[CollectionDefinition(Name)]
public class SessionServiceCollection : ICollectionFixture<SessionServicePostgresFixture>
{
    public const string Name = "SessionService integration tests";
}
