namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using System;
using System.IO;
using System.Threading.Tasks;
using Testcontainers.Keycloak;
using Xunit;

/// <summary>
/// <see cref="IAsyncLifetime"/> fixture for UserService's Keycloak integration tests
/// (HU-23). Unlike <see cref="PostgresContainerFixture{TFactory,TProgram,TDbContext}"/>
/// (shared by every DB-backed service), this one is UserService-specific: UserService
/// has no database, and its only external dependency — Keycloak's real Admin REST
/// API — is exactly what this suite exists to exercise. A mocked
/// <c>IKeycloakAdminClient</c> would never have caught the camelCase-serialization
/// bug documented in <c>KeycloakAdminClient.cs</c> (Keycloak silently ignores
/// PascalCase JSON and returns 2xx anyway), because the bug lives in how a REAL
/// Keycloak deserializes the request body — that's the whole point of testing
/// against Testcontainers here instead of a fake.
///
/// Imports <c>scripts/keycloak/umbral-realm.json</c> as-is — the SAME realm export
/// <c>docker-compose.yml</c> uses for local dev, image tag included
/// (<c>quay.io/keycloak/keycloak:25.0</c>) — so this test proves the real
/// "umbral-backend" client/service-account/role wiring, not a hand-rolled substitute
/// that could silently drift from what actually ships.
///
/// Keycloak (JVM) boots much slower than Postgres. Testcontainers.Keycloak's default
/// wait strategy (log line "Added user '...' to realm '...'" OR an HTTP poll of
/// /health/ready) already accounts for this — no extra timeout wiring needed here,
/// but expect InitializeAsync to take on the order of 30-60s on a cold image pull
/// and 10-20s on a warm one.
/// </summary>
public class UserServiceKeycloakFixture : IAsyncLifetime
{
    private const string RealmName = "umbral";
    private const string AdminClientId = "umbral-backend";
    private const string AdminClientSecret = "umbral-backend-secret-change-me";

    private KeycloakContainer _container = null!;

    public UserServiceApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var realmFilePath = FindRealmFile();

        _container = new KeycloakBuilder("quay.io/keycloak/keycloak:25.0")
            .WithRealm(realmFilePath)
            .Build();

        await _container.StartAsync();

        Factory = new UserServiceApiFactory(
            _container.GetBaseAddress(), RealmName, AdminClientId, AdminClientSecret);
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

    /// <summary>
    /// Walks up from the test assembly's output directory (bin/Debug/net10.0/…)
    /// until it finds the repo root (marked by docker-compose.yml, which lives
    /// next to scripts/) — avoids hardcoding a brittle "../../../../.." chain that
    /// breaks the moment build output layout changes.
    /// </summary>
    private static string FindRealmFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docker-compose.yml")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException(
                "Could not locate repo root (docker-compose.yml) above " + AppContext.BaseDirectory);

        return Path.Combine(dir.FullName, "scripts", "keycloak", "umbral-realm.json");
    }
}

[CollectionDefinition(Name)]
public class UserServiceKeycloakCollection : ICollectionFixture<UserServiceKeycloakFixture>
{
    public const string Name = "UserService Keycloak integration tests";
}
