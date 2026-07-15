namespace UMBRAL_Back_end.IntegrationTests.Missions;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UMBRAL_Back_end.Adapter.Controllers;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Infrastructure.Persistence;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using Xunit;

[Collection(MissionServiceCollection.Name)]
public class MissionPersistenceTests(MissionServicePostgresFixture fixture) : IAsyncLifetime
{
    // xUnit creates a new instance of this class per [Fact], so InitializeAsync runs before
    // EACH test — unlike the collection fixture, which is shared by the whole class. Resetting
    // here keeps tests isolated from rows left over by earlier tests in the same collection.
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateMission_ValidRequestThroughHttp_PersistsInRealPostgres()
    {
        var client = fixture.Factory.CreateClient();
        var request = new CreateMissionRequest(
            $"Operación Alfa {Guid.NewGuid()}", "Descripción de prueba", "Medium", 60);

        var response = await client.PostAsJsonAsync("/api/missions", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var missionId = await response.Content.ReadFromJsonAsync<Guid>();

        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await dbContext.Missions.SingleAsync(m => m.Id == missionId);

        persisted.Name.Should().Be(request.Name);
        persisted.Description.Should().Be(request.Description);
        persisted.Difficulty.Should().Be(DifficultyLevel.Medium);
        persisted.MaxDuration.Should().Be(60);
        persisted.Status.Should().Be(MissionStatus.Inactive);
    }

    [Fact]
    public async Task SaveChanges_DuplicateMissionName_ThrowsDueToUniqueIndexInPostgres()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var duplicateName = $"Misión Duplicada {Guid.NewGuid()}";
        var first = Mission.Create(duplicateName, "Primera descripción", DifficultyLevel.Easy, 30).Value;
        var second = Mission.Create(duplicateName, "Segunda descripción", DifficultyLevel.Hard, 45).Value;

        dbContext.Missions.Add(first);
        await dbContext.SaveChangesAsync();

        dbContext.Missions.Add(second);
        var act = async () => await dbContext.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChanges_NameLongerThanColumnLimit_ThrowsDueToVarcharLengthInPostgres()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var oversizedName = new string('A', MissionName.MaxLength + 1);
        var id = Guid.NewGuid();

        var act = async () => await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "Missions" ("Id", "Name", "Description", "Difficulty", "MaxDuration", "Status", "CreatedAt")
             VALUES ({id}, {oversizedName}, '', 'Easy', 30, 'Inactive', now())
             """);

        (await act.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be("22001");
    }

    [Fact]
    public async Task Database_Migrate_AppliesAllMigrationsCleanlyAndIsIdempotent()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pendingBeforeReRun = await dbContext.Database.GetPendingMigrationsAsync();
        pendingBeforeReRun.Should().BeEmpty();

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        var allMigrations = dbContext.Database.GetMigrations();
        appliedMigrations.Should().BeEquivalentTo(allMigrations);

        var act = async () => await dbContext.Database.MigrateAsync();
        await act.Should().NotThrowAsync();

        (await dbContext.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }
}
