using MassTransit;
using Microsoft.EntityFrameworkCore;
using TeamService.Application.Rankings;
using TeamService.Domain.Rankings;
using TeamService.Domain.Teams;
using TeamService.Infrastructure.Messaging.Consumers;
using TeamService.Infrastructure.Persistence;
using TeamService.Infrastructure.Persistence.Repositories;
using TeamService.Infrastructure.Projections;
using UMBRAL.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddOpenApi();

// ── Database (own isolated DB — Database-per-Service pattern) ─────────────────
builder.Services.AddDbContext<TeamsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── MediatR ───────────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<ITeamRepository, TeamRepository>();

// ── HU-24: CQRS ranking read model ────────────────────────────────────────────
// The projection repository serves the read path (pre-sorted SELECT, no joins).
// The projector keeps the projection in sync with the Team aggregate, sharing
// the same DbContext as the write so both commit in a single transaction.
builder.Services.AddScoped<IRankingProjectionRepository, RankingProjectionRepository>();
builder.Services.AddScoped<IRankingProjector, RankingProjector>();

// ── MassTransit + RabbitMQ ───────────────────────────────────────────────────
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<SessionCancelledConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(new Uri(builder.Configuration.GetConnectionString("RabbitMQ")
                         ?? "amqp://guest:guest@localhost:5672/"));
        // Per-service prefix — keeps the queue namespace uniform with the
        // rest of the bus even though TeamService's consumer name doesn't
        // currently collide with anyone else's.
        cfg.ConfigureEndpoints(ctx, new KebabCaseEndpointNameFormatter(prefix: "team", includeNamespace: false));
    });
});

// ── Keycloak JWT auth (HU-23) ─────────────────────────────────────────────────
builder.Services.AddUmbralJwtAuth(builder.Configuration);

// ── CORS ──────────────────────────────────────────────────────────────────────
// Unificado (dev LAN + orígenes públicos desde Cors:AllowedOrigins).
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
