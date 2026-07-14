namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using UMBRAL_Back_end.Infrastructure.Persistence;

public class MissionServiceApiFactory(string connectionString)
    : IntegrationTestApiFactory<Program, AppDbContext>(connectionString);
