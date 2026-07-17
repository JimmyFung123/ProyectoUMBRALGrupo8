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
/// Level-2 fan-out counterpart of <see cref="StageAddedFanOutFixture"/> for the removal
/// path: <c>StageRemovedIntegrationEvent</c> is consumed independently by MissionService
/// (decrements <c>StageCountLookup.Count</c>) and ClueService (deletes the matching
/// <c>StageLookup</c> row). Structurally identical to <see cref="StageAddedFanOutFixture"/> —
/// see that fixture's remarks for why a real broker (not the in-memory transport used by
/// Level-1) is required to prove the fan-out.
///
/// StageService itself is never started here either: only the wire shape of the event it
/// publishes (see <c>RemoveStageCommandHandler</c>) matters, not its internal behavior.
/// </summary>
public class StageRemovedFanOutFixture : IAsyncLifetime
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
            .WithDatabase("missionservice_l2_removed_it")
            .WithUsername("umbral")
            .WithPassword("umbral")
            .Build();
        _cluePostgres = new PostgreSqlBuilder("postgres:15-alpine")
            .WithDatabase("clueservice_l2_removed_it")
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

        // Forces both hosts (and therefore both StageRemovedConsumers) to start and bind
        // their queues before the test publishes anything — see StageAddedFanOutFixture.
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

        _publisherBus = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(new Uri(rabbitMqConnectionString));
        });
        await _publisherBus.StartAsync();
    }

    /// <summary>Publishes a <see cref="StageRemovedIntegrationEvent"/> exactly as StageService would.</summary>
    public Task PublishStageRemovedAsync(Guid stageId, Guid missionId)
        => _publisherBus.Publish(new StageRemovedIntegrationEvent(stageId, missionId, DateTime.UtcNow));

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
public class StageRemovedFanOutCollection : ICollectionFixture<StageRemovedFanOutFixture>
{
    public const string Name = "StageRemoved fan-out integration tests (Level 2 - real RabbitMQ)";
}
