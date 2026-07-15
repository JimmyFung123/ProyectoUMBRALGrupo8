// SessionService and StageService each generate their own top-level-statements `Program`
// class in the *global* namespace, same conflict handled elsewhere in this project (see
// SessionServiceRabbitMqApiFactory.cs / StageServiceRabbitMqApiFactory.cs). Needed here
// directly because this fixture migrates each service's DbContext itself.
extern alias SessionServiceAssembly;
extern alias StageServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Messaging;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using UMBRAL.Contracts.Events;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using SessionServiceAssembly::SessionService.Infrastructure.Persistence;
using StageServiceAssembly::StageService.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// Level-2 fan-out for <c>MissionActivatedIntegrationEvent</c>: consumed independently by
/// SessionService and StageService, which react asymmetrically on purpose — see
/// <see cref="MissionActivatedFanOutTests"/> for why. Structurally identical to
/// <see cref="StageAddedFanOutFixture"/>; MissionService itself is never started, only the
/// wire shape of the event it publishes (<c>ChangeMissionStatusCommandHandler</c>) matters.
/// </summary>
public class MissionActivatedFanOutFixture : IAsyncLifetime
{
    private RabbitMqContainer _rabbitMq = null!;
    private PostgreSqlContainer _sessionPostgres = null!;
    private PostgreSqlContainer _stagePostgres = null!;
    private IBusControl _publisherBus = null!;

    public SessionServiceRabbitMqApiFactory SessionFactory { get; private set; } = null!;
    public StageServiceRabbitMqApiFactory StageFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _rabbitMq = new RabbitMqBuilder("rabbitmq:3-management-alpine").Build();
        _sessionPostgres = new PostgreSqlBuilder("postgres:15-alpine")
            .WithDatabase("sessionservice_l2_missionactivated_it")
            .WithUsername("umbral")
            .WithPassword("umbral")
            .Build();
        _stagePostgres = new PostgreSqlBuilder("postgres:15-alpine")
            .WithDatabase("stageservice_l2_missionactivated_it")
            .WithUsername("umbral")
            .WithPassword("umbral")
            .Build();

        await Task.WhenAll(
            _rabbitMq.StartAsync(),
            _sessionPostgres.StartAsync(),
            _stagePostgres.StartAsync());

        var rabbitMqConnectionString = _rabbitMq.GetConnectionString();

        SessionFactory = new SessionServiceRabbitMqApiFactory(_sessionPostgres.GetConnectionString(), rabbitMqConnectionString);
        StageFactory = new StageServiceRabbitMqApiFactory(_stagePostgres.GetConnectionString(), rabbitMqConnectionString);

        _ = SessionFactory.Server;
        _ = StageFactory.Server;

        using (var sessionScope = SessionFactory.Services.CreateScope())
        {
            var db = sessionScope.ServiceProvider.GetRequiredService<SessionsDbContext>();
            await db.Database.MigrateAsync();
        }

        using (var stageScope = StageFactory.Services.CreateScope())
        {
            var db = stageScope.ServiceProvider.GetRequiredService<StagesDbContext>();
            await db.Database.MigrateAsync();
        }

        _publisherBus = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(new Uri(rabbitMqConnectionString));
        });
        await _publisherBus.StartAsync();
    }

    /// <summary>Publishes a <see cref="MissionActivatedIntegrationEvent"/> exactly as MissionService would.</summary>
    public Task PublishMissionActivatedAsync(Guid missionId, string name, string difficulty = "Medium")
        => _publisherBus.Publish(new MissionActivatedIntegrationEvent(missionId, name, DateTime.UtcNow, difficulty));

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
                    await StageFactory.DisposeAsync();
                }
                finally
                {
                    try
                    {
                        await _sessionPostgres.DisposeAsync();
                    }
                    finally
                    {
                        try
                        {
                            await _stagePostgres.DisposeAsync();
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
public class MissionActivatedFanOutCollection : ICollectionFixture<MissionActivatedFanOutFixture>
{
    public const string Name = "MissionActivated fan-out integration tests (Level 2 - real RabbitMQ)";
}
