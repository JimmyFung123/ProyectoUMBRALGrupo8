using MassTransit;
using Microsoft.EntityFrameworkCore;
using SessionService.Domain.MissionLookup;
using SessionService.Domain.Sessions;
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
builder.Services.AddScoped<ITeamRepository, TeamRepository>();
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

// ── CORS ──────────────────────────────────────────────────────────────────────
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
app.UseAuthorization();
app.MapControllers();

app.Run();
