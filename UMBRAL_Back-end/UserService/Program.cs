using FluentValidation;
using UMBRAL.Auth;
using UMBRAL.Observability;
using UserService.Application.Users;
using UserService.Infrastructure.Email;
using UserService.Infrastructure.ExternalClients;
using UserService.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddOpenApi();

// ── Trazabilidad por correlación (X-Correlation-ID) ─────────────────────────
builder.Services.AddUmbralCorrelationId();

// ── MediatR ───────────────────────────────────────────────────────────────────
// LoggingBehavior (UMBRAL.Application) envuelve cada command/query con logging
// estructurado y timing — reemplaza las llamadas ILogger dispersas a mano.
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(UMBRAL.Application.LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(UMBRAL.Application.ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// ── External HTTP clients ─────────────────────────────────────────────────────
// El cliente HTTP de Keycloak Admin lee Keycloak:AdminBaseUrl de la config.
builder.Services.AddHttpClient<IKeycloakAdminClient, KeycloakAdminClient>();

// ── Email (SMTP → Mailpit en dev) ─────────────────────────────────────────────
// Envía el correo con la clave temporal al crear un usuario (HU-23).
builder.Services.AddSingleton<IUserEmailSender, SmtpUserEmailSender>();

// ── Generador de la clave temporal (HU-23) ────────────────────────────────────
// El admin no escribe la clave: la genera el sistema y se manda por correo.
builder.Services.AddSingleton<IPasswordGenerator, SecurePasswordGenerator>();

// ── JWT Bearer auth (HU-23) ──────────────────────────────────────────────────
builder.Services.AddUmbralJwtAuth(builder.Configuration);

// ── CORS ──────────────────────────────────────────────────────────────────────
// Unificado (dev LAN + orígenes públicos desde Cors:AllowedOrigins).
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
