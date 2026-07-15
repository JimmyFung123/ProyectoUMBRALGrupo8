using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using StageService.Application;
using StageService.Domain.MissionLookup;
using StageService.Domain.Stages;
using StageService.Infrastructure.Messaging;
using StageService.Infrastructure.Messaging.Consumers;
using StageService.Infrastructure.Persistence;
using StageService.Infrastructure.Persistence.Repositories;
using UMBRAL.Auth;
using UMBRAL.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddOpenApi();

// ── Trazabilidad por correlación (X-Correlation-ID) ─────────────────────────
builder.Services.AddUmbralCorrelationId();

builder.Services.AddDbContext<StagesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(UMBRAL.Application.LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(UMBRAL.Application.ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.AddScoped<IStageRepository, StageRepository>();
builder.Services.AddScoped<IMissionLookupRepository, MissionLookupRepository>();
builder.Services.AddScoped<IIntegrationEventBus, MassTransitIntegrationEventBus>();

// HU-27 — the InternalSyncHealthController calls MissionService over HTTP to
// re-seed MissionsLookup when an admin triggers a manual reproject.
builder.Services.AddHttpClient();

builder.Services.AddUmbralMassTransit(builder.Configuration, "stage", x =>
{
    x.AddConsumer<MissionCreatedConsumer>();
    x.AddConsumer<MissionActivatedConsumer>();
    x.AddConsumer<MissionDeactivatedConsumer>();
});

builder.Services.AddUmbralJwtAuth(builder.Configuration);

// CORS unificado (dev LAN + orígenes públicos desde Cors:AllowedOrigins).
builder.Services.AddUmbralCors(builder.Configuration);

var app = builder.Build();

// Primer middleware: asigna/propaga el correlation id y etiqueta todos los logs.
app.UseUmbralCorrelationId();

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
