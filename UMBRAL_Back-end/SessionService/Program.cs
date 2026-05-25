using MassTransit;
using Microsoft.EntityFrameworkCore;
using SessionService.Application.Sessions;
using SessionService.Domain.MissionLookup;
using SessionService.Domain.Sessions;
using SessionService.Infrastructure.BackgroundServices;
using SessionService.Infrastructure.ExternalClients;
using SessionService.Infrastructure.Hubs;
using SessionService.Infrastructure.Messaging.Consumers;
using SessionService.Infrastructure.Persistence;
using SessionService.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddOpenApi();

// ── Database (own isolated DB — Database-per-Service pattern) ─────────────────
builder.Services.AddDbContext<SessionsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── MediatR ───────────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<ISessionEventRepository, SessionEventRepository>();
builder.Services.AddScoped<IMissionLookupRepository, MissionLookupRepository>();

// ── MassTransit + RabbitMQ (consumer side) ───────────────────────────────────
builder.Services.AddMassTransit(x =>
{
    // Register all three consumers that keep MissionsLookup in sync
    x.AddConsumer<MissionCreatedConsumer>();
    x.AddConsumer<MissionActivatedConsumer>();
    x.AddConsumer<MissionDeactivatedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(new Uri(builder.Configuration.GetConnectionString("RabbitMQ")
                         ?? "amqp://guest:guest@localhost:5672/"));

        // Auto-configure queues/exchanges for all registered consumers
        cfg.ConfigureEndpoints(ctx);
    });
});

// ── External HTTP clients ─────────────────────────────────────────────────────
builder.Services.AddHttpClient<ITeamServiceClient, TeamServiceClient>(client =>
{
    var url = builder.Configuration["TeamServiceUrl"] ?? "http://localhost:5095/";
    client.BaseAddress = new Uri(url);
});

builder.Services.AddHttpClient<IClueServiceClient, ClueServiceClient>(client =>
{
    var url = builder.Configuration["ClueServiceUrl"] ?? "http://localhost:5094/";
    client.BaseAddress = new Uri(url);
});

builder.Services.AddHttpClient<IStageServiceClient, StageServiceClient>(client =>
{
    var url = builder.Configuration["StageServiceUrl"] ?? "http://localhost:5093/";
    client.BaseAddress = new Uri(url);
});

builder.Services.AddScoped<ClueAutoReleaseService>();
builder.Services.AddHostedService<ClueAutoReleaseWorker>();

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── CORS ──────────────────────────────────────────────────────────────────────
// Allows any LAN origin on Vite dev ports (5173/5174) so participants can connect
// from phones on the same network. SetIsOriginAllowed is needed because we keep
// AllowCredentials (SignalR), which is incompatible with AllowAnyOrigin.
builder.Services.AddCors(options =>
    options.AddPolicy("AllowFrontend", policy =>
        policy.SetIsOriginAllowed(origin =>
                {
                    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
                    if (uri.Port != 5173 && uri.Port != 5174) return false;
                    return uri.IsLoopback
                        || uri.Host.StartsWith("192.168.")
                        || uri.Host.StartsWith("10.")
                        || uri.Host.StartsWith("172.");
                })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();
app.MapHub<SessionHub>("/hubs/session");

app.Run();
