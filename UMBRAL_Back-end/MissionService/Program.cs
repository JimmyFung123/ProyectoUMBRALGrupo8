using MassTransit;
using Microsoft.EntityFrameworkCore;
using UMBRAL.Auth;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Infrastructure.Messaging.Consumers;
using UMBRAL_Back_end.Infrastructure.Persistence;
using UMBRAL_Back_end.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddOpenApi();

// PostgreSQL via EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// MediatR — scans this assembly for all IRequestHandler<,> implementations
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Repositories
builder.Services.AddScoped<IMissionRepository, MissionRepository>();
builder.Services.AddScoped<IStageCountLookupRepository, StageCountLookupRepository>();

// HU-27 — InternalSyncHealthController calls StageService over HTTP to rebuild
// the StageCountLookup projection on manual reproject.
builder.Services.AddHttpClient();

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

// CORS for the React dev server (Vite default port)
builder.Services.AddCors(options =>
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()));

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
