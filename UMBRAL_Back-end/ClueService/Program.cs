using MassTransit;
using Microsoft.EntityFrameworkCore;
using ClueService.Domain.Clues;
using ClueService.Domain.StageLookup;
using ClueService.Infrastructure.Messaging.Consumers;
using ClueService.Infrastructure.Persistence;
using ClueService.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddOpenApi();

builder.Services.AddDbContext<CluesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddScoped<IClueRepository, ClueRepository>();
builder.Services.AddScoped<IStageLookupRepository, StageLookupRepository>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<StageAddedConsumer>();
    x.AddConsumer<StageRemovedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(new Uri(builder.Configuration.GetConnectionString("RabbitMQ")
                         ?? "amqp://guest:guest@localhost:5672/"));
        cfg.ConfigureEndpoints(ctx);
    });
});

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
