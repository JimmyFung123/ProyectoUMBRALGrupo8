namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMBRAL_Back_end.Infrastructure.Messaging.Consumers;
using UMBRAL_Back_end.Infrastructure.Persistence;

public class MissionServiceApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (dbContextDescriptor is not null)
                services.Remove(dbContextDescriptor);

            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

            // Program.cs wires AddMassTransit against real RabbitMQ unconditionally.
            // With no broker reachable from the test host, publishing an integration
            // event (CreateMissionCommandHandler -> IPublishEndpoint.Publish) blocks
            // for MassTransit's full retry window (~100s) before the HTTP request
            // under test gets aborted. Swap the transport for the in-memory one so
            // publish/consume stays local to the test process — Level 2 (real
            // RabbitMQ via Testcontainers) is a separate, later piece of work.
            var massTransitDescriptors = services
                .Where(d => (d.ServiceType.Namespace ?? string.Empty)
                    .StartsWith("MassTransit", StringComparison.Ordinal))
                .ToList();

            foreach (var descriptor in massTransitDescriptors)
                services.Remove(descriptor);

            services.AddMassTransit(x =>
            {
                x.AddConsumer<StageAddedConsumer>();
                x.AddConsumer<StageRemovedConsumer>();
                x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
            });
        });
    }
}
