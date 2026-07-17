// SessionService generates its own top-level-statements `Program` class in the *global*
// namespace, same conflict handled elsewhere in this project (see
// SessionServiceRabbitMqApiFactory.cs). Needed here directly because this fixture
// migrates and seeds SessionService's SessionsDbContext itself.
extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Messaging;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using UMBRAL.Contracts.Events;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using SessionServiceAssembly::SessionService.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// Level-2, single-consumer scenario: only SessionService's <c>MissionUpdatedConsumer</c>
/// reacts to <c>MissionUpdatedIntegrationEvent</c> (StageService and ClueService don't
/// consume it). A real RabbitMQ container is still required — this proves the wire
/// shape/serialization/queue-prefix routing over the real broker, which Level-1's
/// in-memory transport does not exercise — but only one Postgres-backed service is
/// started (see <c>StageAddedFanOutFixture</c> remarks for why Level-2 exists at all).
/// MissionService itself is never started, only the wire shape of the event it publishes
/// (<c>UpdateMissionCommandHandler</c>) matters.
/// </summary>
public class MissionUpdatedConsumerFixture : IAsyncLifetime
{
    private RabbitMqContainer _rabbitMq = null!;
    private PostgreSqlContainer _sessionPostgres = null!;
    private IBusControl _publisherBus = null!;

    public SessionServiceRabbitMqApiFactory SessionFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _rabbitMq = new RabbitMqBuilder("rabbitmq:3-management-alpine").Build();
        _sessionPostgres = new PostgreSqlBuilder("postgres:15-alpine")
            .WithDatabase("sessionservice_l2_missionupdated_it")
            .WithUsername("umbral")
            .WithPassword("umbral")
            .Build();

        await Task.WhenAll(
            _rabbitMq.StartAsync(),
            _sessionPostgres.StartAsync());

        var rabbitMqConnectionString = _rabbitMq.GetConnectionString();

        SessionFactory = new SessionServiceRabbitMqApiFactory(_sessionPostgres.GetConnectionString(), rabbitMqConnectionString);

        _ = SessionFactory.Server;

        using (var sessionScope = SessionFactory.Services.CreateScope())
        {
            var db = sessionScope.ServiceProvider.GetRequiredService<SessionsDbContext>();
            await db.Database.MigrateAsync();
        }

        _publisherBus = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(new Uri(rabbitMqConnectionString));
        });
        await _publisherBus.StartAsync();
    }

    /// <summary>Publishes a <see cref="MissionUpdatedIntegrationEvent"/> exactly as MissionService would.</summary>
    public Task PublishMissionUpdatedAsync(Guid missionId, string name, string difficulty)
        => _publisherBus.Publish(new MissionUpdatedIntegrationEvent(missionId, name, difficulty, DateTime.UtcNow));

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
                await SessionFactory.DisposeAsync();
            }
            finally
            {
                try
                {
                    await _sessionPostgres.DisposeAsync();
                }
                finally
                {
                    await _rabbitMq.DisposeAsync();
                }
            }
        }
    }
}

[CollectionDefinition(Name)]
public class MissionUpdatedConsumerCollection : ICollectionFixture<MissionUpdatedConsumerFixture>
{
    public const string Name = "MissionUpdated consumer integration tests (Level 2 - real RabbitMQ)";
}
