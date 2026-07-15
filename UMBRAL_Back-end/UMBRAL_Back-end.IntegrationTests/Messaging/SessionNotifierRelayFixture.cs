// SessionService generates its own top-level-statements `Program` class in the *global*
// namespace, same conflict handled elsewhere in this project (see
// SessionServiceRabbitMqApiFactory.cs). Needed here directly because this fixture
// migrates SessionService's SessionsDbContext and overrides its ISessionNotifier registration.
extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Messaging;

using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using SessionServiceAssembly::SessionService.Application.Sessions;
using SessionServiceAssembly::SessionService.Infrastructure.Persistence;
using SessionProgram = SessionServiceAssembly::Program;
using Xunit;

/// <summary>
/// Level-2, shared fixture for the six SessionService consumers that ONLY relay to
/// <see cref="ISessionNotifier"/> (SignalR) without touching any DbContext:
/// OperatorMessageBroadcastConsumer, SessionStateChangedConsumer, StageCompletedConsumer,
/// TeamPenalizedConsumer, TriviaWrongAnswerConsumer and ClueReleasedConsumer. Since none of
/// them persist anything, DB polling is impossible — instead this fixture registers
/// <see cref="RecordingSessionNotifierSpy"/> in place of the real SignalR notifier so each
/// test can poll the spy's recorded calls.
///
/// A single RabbitMQ + Postgres pair is shared by all six <c>[Fact]</c>s in
/// <see cref="SessionNotifierRelayTests"/> rather than one fixture per consumer, matching
/// the "no 13 separate container spin-ups" grouping used across this test suite.
/// </summary>
public class SessionNotifierRelayFixture : IAsyncLifetime
{
    private RabbitMqContainer _rabbitMq = null!;
    private PostgreSqlContainer _sessionPostgres = null!;
    private IBusControl _publisherBus = null!;

    public WebApplicationFactory<SessionProgram> SessionFactory { get; private set; } = null!;
    public RecordingSessionNotifierSpy NotifierSpy { get; } = new();

    public async Task InitializeAsync()
    {
        _rabbitMq = new RabbitMqBuilder("rabbitmq:3-management-alpine").Build();
        _sessionPostgres = new PostgreSqlBuilder("postgres:15-alpine")
            .WithDatabase("sessionservice_l2_notifierrelay_it")
            .WithUsername("umbral")
            .WithPassword("umbral")
            .Build();

        await Task.WhenAll(
            _rabbitMq.StartAsync(),
            _sessionPostgres.StartAsync());

        var rabbitMqConnectionString = _rabbitMq.GetConnectionString();

        // WithWebHostBuilder layers this additional ISessionNotifier override on top of
        // SessionServiceRabbitMqApiFactory's existing RabbitMQ/DbContext wiring rather than
        // requiring a dedicated subclass just for this one fixture.
        SessionFactory = new SessionServiceRabbitMqApiFactory(_sessionPostgres.GetConnectionString(), rabbitMqConnectionString)
            .WithWebHostBuilder((IWebHostBuilder builder) => builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISessionNotifier>();
                services.AddSingleton<ISessionNotifier>(NotifierSpy);
            }));

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

    /// <summary>Publishes any integration event exactly as the owning service would.</summary>
    public Task PublishAsync<TEvent>(TEvent evt) where TEvent : class
        => _publisherBus.Publish(evt);

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
public class SessionNotifierRelayCollection : ICollectionFixture<SessionNotifierRelayFixture>
{
    public const string Name = "SessionNotifier relay consumer integration tests (Level 2 - real RabbitMQ)";
}
