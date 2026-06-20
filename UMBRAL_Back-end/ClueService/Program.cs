using MassTransit;
using Microsoft.EntityFrameworkCore;
using ClueService.Application;
using ClueService.Domain.Clues;
using ClueService.Domain.StageLookup;
using ClueService.Infrastructure.Messaging;
using ClueService.Infrastructure.Messaging.Consumers;
using ClueService.Infrastructure.Persistence;
using ClueService.Infrastructure.Persistence.Repositories;
using UMBRAL.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddOpenApi();

builder.Services.AddDbContext<CluesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddScoped<IClueRepository, ClueRepository>();
builder.Services.AddScoped<IStageLookupRepository, StageLookupRepository>();
builder.Services.AddScoped<IIntegrationEventBus, MassTransitIntegrationEventBus>();

// HU-27 — InternalSyncHealthController calls StageService over HTTP to rebuild
// the StageLookup projection on manual reproject.
builder.Services.AddHttpClient();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<StageAddedConsumer>();
    x.AddConsumer<StageRemovedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(new Uri(builder.Configuration.GetConnectionString("RabbitMQ")
                         ?? "amqp://guest:guest@localhost:5672/"));
        // Per-service prefix so this service's stage-event consumers don't
        // share a queue with MissionService's (fan-out, not load balancing).
        cfg.ConfigureEndpoints(ctx, new KebabCaseEndpointNameFormatter(prefix: "clue", includeNamespace: false));
    });
});

builder.Services.AddUmbralJwtAuth(builder.Configuration);

// CORS unificado (dev LAN + orígenes públicos desde Cors:AllowedOrigins).
builder.Services.AddUmbralCors(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
