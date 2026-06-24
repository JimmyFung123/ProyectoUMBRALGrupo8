using MassTransit;
using Microsoft.EntityFrameworkCore;
using UMBRAL.Auth;
using UMBRAL_Back_end.Application;
using UMBRAL_Back_end.Application.Missions;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Infrastructure.ExternalClients;
using UMBRAL_Back_end.Infrastructure.Messaging;
using UMBRAL_Back_end.Infrastructure.Messaging.Consumers;
using UMBRAL_Back_end.Infrastructure.Persistence;
using UMBRAL_Back_end.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddScoped<IMissionRepository, MissionRepository>();
builder.Services.AddScoped<IStageCountLookupRepository, StageCountLookupRepository>();
builder.Services.AddScoped<IIntegrationEventBus, MassTransitIntegrationEventBus>();

// HU-27 — InternalSyncHealthController calls StageService over HTTP to rebuild
// the StageCountLookup projection on manual reproject.
builder.Services.AddHttpClient();

// RB-15 — SessionServiceClient calls SessionService to check for active sessions
// before allowing mission deactivation.
builder.Services.AddHttpClient<ISessionServiceClient, SessionServiceClient>(client =>
{
    var url = builder.Configuration["SessionServiceUrl"] ?? "http://localhost:5092/";
    client.BaseAddress = new Uri(url);
});

// MassTransit — MissionService publishes integration events to RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<StageAddedConsumer>();
    x.AddConsumer<StageRemovedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(new Uri(builder.Configuration.GetConnectionString("RabbitMQ")
                         ?? "amqp://guest:guest@localhost:5672/"));
        // Per-service prefix so this service's stage-event consumers don't
        // share a queue with ClueService's (fan-out, not load balancing).
        cfg.ConfigureEndpoints(ctx, new KebabCaseEndpointNameFormatter(prefix: "mission", includeNamespace: false));
    });
});

// Keycloak JWT auth (HU-23) — optional until [Authorize] is applied per-endpoint.
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
