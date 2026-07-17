using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ClueService.Application;
using ClueService.Domain.Clues;
using ClueService.Domain.StageLookup;
using ClueService.Infrastructure.Messaging;
using ClueService.Infrastructure.Messaging.Consumers;
using ClueService.Infrastructure.Persistence;
using ClueService.Infrastructure.Persistence.Repositories;
using UMBRAL.Auth;
using UMBRAL.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddOpenApi();

// ── Trazabilidad por correlación (X-Correlation-ID) ─────────────────────────
builder.Services.AddUmbralCorrelationId();

builder.Services.AddDbContext<CluesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(UMBRAL.Application.LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(UMBRAL.Application.ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.AddScoped<IClueRepository, ClueRepository>();
builder.Services.AddScoped<IStageLookupRepository, StageLookupRepository>();
builder.Services.AddScoped<IIntegrationEventBus, MassTransitIntegrationEventBus>();

// HU-27 — InternalSyncHealthController calls StageService over HTTP to rebuild
// the StageLookup projection on manual reproject.
builder.Services.AddHttpClient();

builder.Services.AddUmbralMassTransit(builder.Configuration, "clue", x =>
{
    x.AddConsumer<StageAddedConsumer>();
    x.AddConsumer<StageRemovedConsumer>();
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
