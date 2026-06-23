using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using UMBRAL.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddUmbralJwtAuth(builder.Configuration);
builder.Services.AddUmbralCors(builder.Configuration);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("authenticated", policy =>
        policy.RequireAuthenticatedUser());
});

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("general", windowOptions =>
    {
        windowOptions.Window = TimeSpan.FromSeconds(10);
        windowOptions.PermitLimit = 50;
        windowOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        windowOptions.QueueLimit = 10;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();

app.Run();
