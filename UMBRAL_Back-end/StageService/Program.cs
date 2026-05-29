using MassTransit;
using Microsoft.EntityFrameworkCore;
using StageService.Domain.MissionLookup;
using StageService.Domain.Stages;
using StageService.Infrastructure.Messaging.Consumers;
using StageService.Infrastructure.Persistence;
using StageService.Infrastructure.Persistence.Repositories;
using UMBRAL.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddOpenApi();

builder.Services.AddDbContext<StagesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddScoped<IStageRepository, StageRepository>();
builder.Services.AddScoped<IMissionLookupRepository, MissionLookupRepository>();

// HU-27 — the InternalSyncHealthController calls MissionService over HTTP to
// re-seed MissionsLookup when an admin triggers a manual reproject.
builder.Services.AddHttpClient();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<MissionCreatedConsumer>();
    x.AddConsumer<MissionActivatedConsumer>();
    x.AddConsumer<MissionDeactivatedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(new Uri(builder.Configuration.GetConnectionString("RabbitMQ")
                         ?? "amqp://guest:guest@localhost:5672/"));
        // Per-service prefix so this service's MissionsLookup consumers don't
        // share a queue with SessionService's (fan-out, not load balancing).
        cfg.ConfigureEndpoints(ctx, new KebabCaseEndpointNameFormatter(prefix: "stage", includeNamespace: false));
    });
});

builder.Services.AddUmbralJwtAuth(builder.Configuration);

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
