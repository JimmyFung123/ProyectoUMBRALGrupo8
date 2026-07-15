namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using UMBRAL_Back_end.Infrastructure.Persistence;

/// <summary>Level-2: MissionService wired to a real Testcontainers RabbitMQ broker.</summary>
public class MissionServiceRabbitMqApiFactory(string connectionString, string rabbitMqConnectionString)
    : RabbitMqBackedApiFactory<Program, AppDbContext>(connectionString, rabbitMqConnectionString);
