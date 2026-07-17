// ClueService generates its own top-level-statements `Program` class in the *global*
// namespace, same conflict handled elsewhere in this project (see
// ClueServiceApiFactory.cs / ClueServiceRabbitMqApiFactory.cs). Needed here directly
// because this fixture migrates ClueService's CluesDbContext itself.
extern alias ClueServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Messaging;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using UMBRAL.Contracts.Events;
using UMBRAL_Back_end.Infrastructure.Persistence;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using ClueServiceAssembly::ClueService.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// Level-2 fixture for the StageAdded fan-out scenario. Level-1 only proves persistence
/// against real Postgres, always over MassTransit's in-memory transport — it never
/// exercises real RabbitMQ routing, serialization, or the per-service queue-prefix design
/// in <c>AddUmbralMassTransit</c> (the "mission"/"clue" prefixes passed from each
/// service's Program.cs) that exists specifically so two services can each consume the
/// same integration event type without stealing each other's messages in a fan-out.
///
/// This fixture starts: one real RabbitMQ container (shared), one Postgres container +
/// <see cref="MissionServiceRabbitMqApiFactory"/>, and one Postgres container +
/// <see cref="ClueServiceRabbitMqApiFactory"/> — both services pointed at the same real
/// broker. StageService itself is never started: nothing in this scenario depends on its
/// internal behavior, only on the wire shape of the event it is known to publish
/// (<see cref="StageAddedIntegrationEvent"/>). A throwaway MassTransit bus with no
/// consumers stands in for it via <see cref="PublishStageAddedAsync"/>.
/// </summary>
public class StageAddedFanOutFixture : IAsyncLifetime
{
    private RabbitMqContainer _rabbitMq = null!;
    private PostgreSqlContainer _missionPostgres = null!;
    private PostgreSqlContainer _cluePostgres = null!;
    private IBusControl _publisherBus = null!;

    public MissionServiceRabbitMqApiFactory MissionFactory { get; private set; } = null!;
    public ClueServiceRabbitMqApiFactory ClueFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _rabbitMq = new RabbitMqBuilder("rabbitmq:3-management-alpine").Build();
        _missionPostgres = new PostgreSqlBuilder("postgres:15-alpine")
            .WithDatabase("missionservice_l2_it")
            .WithUsername("umbral")
            .WithPassword("umbral")
            .Build();
        _cluePostgres = new PostgreSqlBuilder("postgres:15-alpine")
            .WithDatabase("clueservice_l2_it")
            .WithUsername("umbral")
            .WithPassword("umbral")
            .Build();

        await Task.WhenAll(
            _rabbitMq.StartAsync(),
            _missionPostgres.StartAsync(),
            _cluePostgres.StartAsync());

        var rabbitMqConnectionString = _rabbitMq.GetConnectionString();

        MissionFactory = new MissionServiceRabbitMqApiFactory(_missionPostgres.GetConnectionString(), rabbitMqConnectionString);
        ClueFactory = new ClueServiceRabbitMqApiFactory(_cluePostgres.GetConnectionString(), rabbitMqConnectionString);

        // WebApplicationFactory builds its host lazily on first access. Touching .Server
        // here forces both hosts (and therefore MassTransit's bus + both
        // StageAddedConsumers) to start and connect to RabbitMQ NOW, before the test
        // publishes anything — otherwise the publish could race a consumer that hasn't
        // finished binding its queue yet.
        _ = MissionFactory.Server;
        _ = ClueFactory.Server;

        using (var missionScope = MissionFactory.Services.CreateScope())
        {
            var db = missionScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
        }

        using (var clueScope = ClueFactory.Services.CreateScope())
        {
            var db = clueScope.ServiceProvider.GetRequiredService<CluesDbContext>();
            await db.Database.MigrateAsync();
        }

        // Stand-in for StageService: a bare MassTransit bus with no consumers, only used
        // to publish onto the same real broker the two services above are listening on.
        _publisherBus = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(new Uri(rabbitMqConnectionString));
        });
        await _publisherBus.StartAsync();
    }

    /// <summary>Publishes a <see cref="StageAddedIntegrationEvent"/> exactly as StageService would.</summary>
    public Task PublishStageAddedAsync(Guid stageId, Guid missionId, string stageType)
        => _publisherBus.Publish(new StageAddedIntegrationEvent(stageId, missionId, stageType, DateTime.UtcNow));

    public async Task DisposeAsync()
    {
        try
        {
            await _publisherBus.StopAsync();
        }
        finally
        {
            try
            {
                await MissionFactory.DisposeAsync();
            }
            finally
            {
                try
                {
                    await ClueFactory.DisposeAsync();
                }
                finally
                {
                    try
                    {
                        await _missionPostgres.DisposeAsync();
                    }
                    finally
                    {
                        try
                        {
                            await _cluePostgres.DisposeAsync();
                        }
                        finally
                        {
                            await _rabbitMq.DisposeAsync();
                        }
                    }
                }
            }
        }
    }
}

[CollectionDefinition(Name)]
public class StageAddedFanOutCollection : ICollectionFixture<StageAddedFanOutFixture>
{
    public const string Name = "StageAdded fan-out integration tests (Level 2 - real RabbitMQ)";
}
