using MassTransit;
using Microsoft.EntityFrameworkCore;
using SessionService.Application.Sessions;
using SessionService.Application.Statistics;
using SessionService.Domain.MissionLookup;
using SessionService.Domain.Sessions;
using SessionService.Domain.Statistics;
using SessionService.Infrastructure.BackgroundServices;
using SessionService.Infrastructure.ExternalClients;
using SessionService.Infrastructure.Hubs;
using SessionService.Infrastructure.Messaging.Consumers;
using SessionService.Infrastructure.Persistence;
using SessionService.Infrastructure.Persistence.Repositories;
using UMBRAL.Auth;

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

// ── HU-25: analytics fact table + dashboard read model ──────────────────────
// Write side is hit by gameplay handlers (one INSERT per stage transition).
// Read side is hit only by the admin dashboard query and never blocks the
// write path (separate index, AsNoTracking, no JOINs against Sessions/Teams).
builder.Services.AddScoped<IStageCompletionRecordRepository, StageCompletionRecordRepository>();
builder.Services.AddScoped<IStatisticsReadRepository, StatisticsReadRepository>();

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
// Tighter ping schedule than the default (15 s keep-alive / 30 s client timeout)
// so the participant front shows the "Reconectando…" badge within ~6 s of a
// network drop instead of feeling frozen for half a minute. The 3 s / 6 s ratio
// is the smallest pair SignalR recommends (timeout >= 2 * keep-alive) that
// still tolerates WiFi/4G jitter spikes without triggering false reconnects.
builder.Services.AddSignalR(options =>
{
    options.KeepAliveInterval = TimeSpan.FromSeconds(3);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(6);
});

// ── Keycloak JWT auth (HU-23) ─────────────────────────────────────────────────
// Optional: endpoints stay public unless decorated with [Authorize]. When a
// Bearer token is present we validate it against the umbral realm and expose
// the operator's identity via HttpContext.User for the audit log.
builder.Services.AddUmbralJwtAuth(builder.Configuration);

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<SessionHub>("/hubs/session");

app.Run();
