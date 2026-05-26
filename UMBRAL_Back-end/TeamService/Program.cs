using MassTransit;
using Microsoft.EntityFrameworkCore;
using TeamService.Domain.Teams;
using TeamService.Infrastructure.Messaging.Consumers;
using TeamService.Infrastructure.Persistence;
using TeamService.Infrastructure.Persistence.Repositories;
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

// ── MassTransit + RabbitMQ ───────────────────────────────────────────────────
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<SessionCancelledConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(new Uri(builder.Configuration.GetConnectionString("RabbitMQ")
                         ?? "amqp://guest:guest@localhost:5672/"));
        cfg.ConfigureEndpoints(ctx);
    });
});

// ── Keycloak JWT auth (HU-23) ─────────────────────────────────────────────────
builder.Services.AddUmbralJwtAuth(builder.Configuration);

// ── CORS ──────────────────────────────────────────────────────────────────────
// Allows any LAN origin on Vite dev ports (5173/5174) so participants can connect
// from phones on the same network.
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
