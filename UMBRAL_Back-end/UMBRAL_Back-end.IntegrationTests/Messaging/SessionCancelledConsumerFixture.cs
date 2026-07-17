// TeamService generates its own top-level-statements `Program` class in the *global*
// namespace, same conflict handled elsewhere in this project (see
// TeamServiceRabbitMqApiFactory.cs). Needed here directly because this fixture
// migrates and seeds TeamService's TeamsDbContext itself.
extern alias TeamServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Messaging;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using UMBRAL.Contracts.Events;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using TeamServiceAssembly::TeamService.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// Level-2, cross-service scenario: SessionService publishes
/// <c>SessionCancelledIntegrationEvent</c> in production, but only TeamService's own
/// <c>SessionCancelledConsumer</c> is under test here — SessionService itself is never
/// started, same pattern as StageService standing out of <see cref="StageAddedFanOutFixture"/>.
/// A throwaway bus with no consumers publishes the raw event onto the real broker.
/// </summary>
public class SessionCancelledConsumerFixture : IAsyncLifetime
{
    private RabbitMqContainer _rabbitMq = null!;
    private PostgreSqlContainer _teamPostgres = null!;
    private IBusControl _publisherBus = null!;

    public TeamServiceRabbitMqApiFactory TeamFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _rabbitMq = new RabbitMqBuilder("rabbitmq:3-management-alpine").Build();
        _teamPostgres = new PostgreSqlBuilder("postgres:15-alpine")
            .WithDatabase("teamservice_l2_sessioncancelled_it")
            .WithUsername("umbral")
            .WithPassword("umbral")
            .Build();

        await Task.WhenAll(
            _rabbitMq.StartAsync(),
            _teamPostgres.StartAsync());

        var rabbitMqConnectionString = _rabbitMq.GetConnectionString();

        TeamFactory = new TeamServiceRabbitMqApiFactory(_teamPostgres.GetConnectionString(), rabbitMqConnectionString);

        _ = TeamFactory.Server;

        using (var teamScope = TeamFactory.Services.CreateScope())
        {
            var db = teamScope.ServiceProvider.GetRequiredService<TeamsDbContext>();
            await db.Database.MigrateAsync();
        }

        _publisherBus = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(new Uri(rabbitMqConnectionString));
        });
        await _publisherBus.StartAsync();
    }

    /// <summary>Publishes a <see cref="SessionCancelledIntegrationEvent"/> exactly as SessionService would.</summary>
    public Task PublishSessionCancelledAsync(Guid sessionId)
        => _publisherBus.Publish(new SessionCancelledIntegrationEvent(sessionId, DateTime.UtcNow));

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
                await TeamFactory.DisposeAsync();
            }
            finally
            {
                try
                {
                    await _teamPostgres.DisposeAsync();
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
public class SessionCancelledConsumerCollection : ICollectionFixture<SessionCancelledConsumerFixture>
{
    public const string Name = "SessionCancelled consumer integration tests (Level 2 - real RabbitMQ)";
}
