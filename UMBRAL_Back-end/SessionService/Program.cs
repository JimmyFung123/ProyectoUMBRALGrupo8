using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SessionService.Application.Missions.Queries.GetMissionStructure;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Facade;
using SessionService.Application.Statistics;
using SessionService.Application.SyncHealth;
using SessionService.Domain.MissionLookup;
using SessionService.Domain.Sessions;
using SessionService.Domain.Statistics;
using SessionService.Application;
using SessionService.Infrastructure.BackgroundServices;
using SessionService.Infrastructure.ExternalClients;
using SessionService.Infrastructure.Hubs;
using SessionService.Infrastructure.Messaging;
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
// HU-26: SessionEventImmutabilityInterceptor blocks any Modified/Deleted change
// on SessionEvent rows so the command audit log stays append-only.
builder.Services.AddSingleton<SessionEventImmutabilityInterceptor>();
builder.Services.AddDbContext<SessionsDbContext>((sp, options) =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .AddInterceptors(sp.GetRequiredService<SessionEventImmutabilityInterceptor>()));

// ── MediatR ───────────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<ISessionEventRepository, SessionEventRepository>();

// ── Application facades (GoF Facade) ───────────────────────────────────────
// Punto de entrada único que orquesta sesión + TeamService + StageService para
// resolver la etapa actual de un participante (usado por GetParticipantStage).
builder.Services.AddScoped<IParticipantStageFacade, ParticipantStageFacade>();
builder.Services.AddScoped<IMissionLookupRepository, MissionLookupRepository>();

// Builder del arbol Composite de la estructura de misiones (extraido del handler por SRP:
// cruza StageService + ClueService para armar Mission -> Stages -> Clues).
builder.Services.AddScoped<IMissionStructureTreeBuilder, MissionStructureTreeBuilder>();

// ── HU-25: analytics fact table + dashboard read model ──────────────────────
// Write side is hit by gameplay handlers (one INSERT per stage transition).
// Read side is hit only by the admin dashboard query and never blocks the
// write path (separate index, AsNoTracking, no JOINs against Sessions/Teams).
builder.Services.AddScoped<IStageCompletionRecordRepository, StageCompletionRecordRepository>();
builder.Services.AddScoped<IStatisticsReadRepository, StatisticsReadRepository>();

// ── MassTransit + RabbitMQ (consumer side) ───────────────────────────────────
// Per-service queue prefix: forces each service to bind its own queue to the
// shared event exchange so the bus behaves as fan-out (every service receives
// every event) instead of competing consumers (RabbitMQ load-balances events
// between same-named queues). Without the prefix, SessionService and
// StageService both register a "MissionCreated" queue and RabbitMQ splits the
// events between them, leaving each MissionsLookup behind by ~50%.
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<MissionCreatedConsumer>();
    x.AddConsumer<MissionActivatedConsumer>();
    x.AddConsumer<MissionDeactivatedConsumer>();
    x.AddConsumer<MissionUpdatedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(new Uri(builder.Configuration.GetConnectionString("RabbitMQ")
                         ?? "amqp://guest:guest@localhost:5672/"));

        // Auto-configure queues/exchanges for all registered consumers, but
        // with a per-service prefix so the queue names cannot collide with
        // the other services that consume the same integration events.
        cfg.ConfigureEndpoints(ctx, new KebabCaseEndpointNameFormatter(prefix: "session", includeNamespace: false));
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

// GoF Proxy: el StageServiceClient real se registra con su HttpClient tipado, pero
// IStageServiceClient se expone como un CachedStageServiceProxy que lo envuelve y cachea
// GetStageWithOptionsAsync. AddMemoryCache registra IMemoryCache como singleton, así la
// caché se comparte entre peticiones/equipos (única forma de que sirva de algo). Los
// consumidores (fachada, handlers de evidencia, GetReleasedClues, estructura del Composite)
// siguen pidiendo IStageServiceClient sin enterarse de la caché.
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<StageServiceClient>(client =>
{
    var url = builder.Configuration["StageServiceUrl"] ?? "http://localhost:5093/";
    client.BaseAddress = new Uri(url);
});
builder.Services.AddScoped<IStageServiceClient>(sp =>
    new CachedStageServiceProxy(
        sp.GetRequiredService<StageServiceClient>(),
        sp.GetRequiredService<IMemoryCache>()));

// ── HU-27: sync-health aggregator — typed clients to every downstream service
//          plus the local EF-backed reader for SessionService's own projections
builder.Services.AddScoped<ILocalSyncHealthReader, LocalSyncHealthReader>();

builder.Services.AddHttpClient<IMissionServiceSyncClient, MissionServiceSyncClient>(client =>
{
    var url = builder.Configuration["MissionServiceUrl"] ?? "http://localhost:5091/";
    client.BaseAddress = new Uri(url);
});

builder.Services.AddHttpClient<IStageServiceSyncClient, StageServiceSyncClient>(client =>
{
    var url = builder.Configuration["StageServiceUrl"] ?? "http://localhost:5093/";
    client.BaseAddress = new Uri(url);
});

builder.Services.AddHttpClient<IClueServiceSyncClient, ClueServiceSyncClient>(client =>
{
    var url = builder.Configuration["ClueServiceUrl"] ?? "http://localhost:5094/";
    client.BaseAddress = new Uri(url);
});

builder.Services.AddHttpClient<ITeamServiceSyncClient, TeamServiceSyncClient>(client =>
{
    var url = builder.Configuration["TeamServiceUrl"] ?? "http://localhost:5095/";
    client.BaseAddress = new Uri(url);
});

builder.Services.AddScoped<ClueAutoReleaseService>();
builder.Services.AddHostedService<ClueAutoReleaseWorker>();

// ── SignalR ───────────────────────────────────────────────────────────────────
// ISessionNotifier decouples Application handlers from SignalR infrastructure.
// The concrete implementation lives in Infrastructure and is invisible to the Application layer.
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
builder.Services.AddScoped<ISessionNotifier, SignalRSessionNotifier>();
builder.Services.AddScoped<IIntegrationEventBus, MassTransitIntegrationEventBus>();

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
