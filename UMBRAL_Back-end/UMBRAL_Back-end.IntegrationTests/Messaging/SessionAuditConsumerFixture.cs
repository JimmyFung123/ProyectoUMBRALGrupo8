// SessionService generates its own top-level-statements `Program` class in the *global*
// namespace, same conflict handled elsewhere in this project (see
// SessionServiceRabbitMqApiFactory.cs). Needed here directly because this fixture
// migrates SessionService's SessionsDbContext itself.
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
/// Level-2, single-consumer scenario for SessionService's <c>SessionAuditConsumer</c>.
/// The event is published directly here via a throwaway bus rather than routed through
/// one of the 14 command handlers that emit it in production — the consumer's own logic
/// (persist a <c>SessionEvent</c>) is identical regardless of caller, same approach as
/// <see cref="StageAddedFanOutFixture"/> standing in for StageService.
/// </summary>
public class SessionAuditConsumerFixture : IAsyncLifetime
{
    private RabbitMqContainer _rabbitMq = null!;
    private PostgreSqlContainer _sessionPostgres = null!;
    private IBusControl _publisherBus = null!;

    public SessionServiceRabbitMqApiFactory SessionFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _rabbitMq = new RabbitMqBuilder("rabbitmq:3-management-alpine").Build();
        _sessionPostgres = new PostgreSqlBuilder("postgres:15-alpine")
            .WithDatabase("sessionservice_l2_sessionaudit_it")
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

    /// <summary>Publishes a <see cref="SessionAuditIntegrationEvent"/> exactly as a SessionService command handler would.</summary>
    public Task PublishSessionAuditAsync(
        Guid sessionId, string description, string? actorName = null, string? commandType = null, string? outcome = null)
        => _publisherBus.Publish(new SessionAuditIntegrationEvent(
            sessionId, description, actorName, commandType, outcome, DateTime.UtcNow));

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
public class SessionAuditConsumerCollection : ICollectionFixture<SessionAuditConsumerFixture>
{
    public const string Name = "SessionAudit consumer integration tests (Level 2 - real RabbitMQ)";
}
