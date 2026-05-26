using UMBRAL.Auth;
using UserService.Application.Users;
using UserService.Infrastructure.ExternalClients;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddOpenApi();

// ── MediatR ───────────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// ── External HTTP clients ─────────────────────────────────────────────────────
// El cliente HTTP de Keycloak Admin lee Keycloak:AdminBaseUrl de la config.
builder.Services.AddHttpClient<IKeycloakAdminClient, KeycloakAdminClient>();

// ── JWT Bearer auth (HU-23) ──────────────────────────────────────────────────
builder.Services.AddUmbralJwtAuth(builder.Configuration);

// ── CORS ──────────────────────────────────────────────────────────────────────
// Solo el front operador (5173) necesita gestionar personal.
builder.Services.AddCors(options =>
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:5173")
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

app.Run();
