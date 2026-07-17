namespace UMBRAL_Back_end.IntegrationTests.Missions;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using UMBRAL_Back_end.Adapter.Controllers;
using UMBRAL_Back_end.Application.Missions;
using UMBRAL_Back_end.Application.Missions.Queries.GetMissionById;
using UMBRAL_Back_end.Application.Missions.Queries.GetMissions;
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> CreateMissionAsync(
        HttpClient client, string? name = null, string difficulty = "Medium", int maxDuration = 60)
    {
        var request = new CreateMissionRequest(
            name ?? $"Misión {Guid.NewGuid()}", "Descripción de prueba", difficulty, maxDuration);
        var response = await client.PostAsJsonAsync("/api/missions", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created, because: await Body(response));
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>
    /// Cliente cuyo <see cref="ISessionServiceClient"/> está reemplazado por un doble
    /// que responde el valor dado, sin tocar la red. Update/Deactivate consultan a
    /// SessionService (RB-14/RB-15) y el cliente real, al no alcanzarlo en aislamiento,
    /// hace fail-closed devolviendo "true" (asume sesiones activas) — lo que bloquearía
    /// esos flujos. El doble nos deja probar tanto el happy path (false) como el
    /// rechazo por sesiones activas (true) de forma determinista.
    /// </summary>
    private HttpClient ClientWithActiveSessions(bool hasActiveSessions)
        => fixture.Factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.RemoveAll<ISessionServiceClient>();
            services.AddSingleton<ISessionServiceClient>(new FakeSessionServiceClient(hasActiveSessions));
        })).CreateClient();

    private static async Task<string> Body(HttpResponseMessage r)
        => $"status inesperado; body: {await r.Content.ReadAsStringAsync()}";

    // ── Create + constraints + migración (base original) ────────────────────────

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

    // ── GET /api/missions/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task GetMissionById_ExistingMission_ReturnsDetailFromPostgres()
    {
        var client = fixture.Factory.CreateClient();
        var missionId = await CreateMissionAsync(client, "Misión Detalle", "Hard", 45);

        var response = await client.GetAsync($"/api/missions/{missionId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await response.Content.ReadFromJsonAsync<MissionDetailDto>();
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(missionId);
        detail.Name.Should().Be("Misión Detalle");
        detail.Difficulty.Should().Be("Hard");
        detail.MaxDuration.Should().Be(45);
        detail.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task GetMissionById_UnknownId_ReturnsNotFound()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/api/missions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/missions (lista + filtro por estado) ───────────────────────────

    [Fact]
    public async Task GetMissions_ReturnsAllPersistedMissions()
    {
        var client = fixture.Factory.CreateClient();
        var firstId = await CreateMissionAsync(client, $"Alfa {Guid.NewGuid()}");
        var secondId = await CreateMissionAsync(client, $"Beta {Guid.NewGuid()}");

        var missions = await client.GetFromJsonAsync<List<MissionDto>>("/api/missions");

        missions.Should().NotBeNull();
        missions!.Select(m => m.Id).Should().Contain(new[] { firstId, secondId });
    }

    [Fact]
    public async Task GetMissions_FilterByStatus_ReturnsOnlyMatching()
    {
        var client = fixture.Factory.CreateClient();
        // Recién creada, la misión está Inactive.
        var missionId = await CreateMissionAsync(client);

        var inactive = await client.GetFromJsonAsync<List<MissionDto>>("/api/missions?status=Inactive");
        inactive!.Select(m => m.Id).Should().Contain(missionId);

        var active = await client.GetFromJsonAsync<List<MissionDto>>("/api/missions?status=Active");
        active!.Select(m => m.Id).Should().NotContain(missionId);
    }

    // ── PUT /api/missions/{id} (actualizar) ─────────────────────────────────────

    [Fact]
    public async Task UpdateMission_NoActiveSessions_PersistsChangeInPostgres()
    {
        var client = ClientWithActiveSessions(hasActiveSessions: false);
        var missionId = await CreateMissionAsync(client, "Nombre Original", "Easy", 30);

        var update = new UpdateMissionRequest("Nombre Actualizado", "Nueva descripción", "Hard", 90);
        var response = await client.PutAsJsonAsync($"/api/missions/{missionId}", update);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, because: await Body(response));

        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await dbContext.Missions.SingleAsync(m => m.Id == missionId);
        persisted.Name.Should().Be("Nombre Actualizado");
        persisted.Difficulty.Should().Be(DifficultyLevel.Hard);
        persisted.MaxDuration.Should().Be(90);
        persisted.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateMission_UnknownId_ReturnsBadRequest()
    {
        var client = fixture.Factory.CreateClient();

        var update = new UpdateMissionRequest("X", "Y", "Easy", 30);
        var response = await client.PutAsJsonAsync($"/api/missions/{Guid.NewGuid()}", update);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateMission_WithActiveSessions_IsRejected()
    {
        // RB-14: no se puede modificar la misión si tiene sesiones activas.
        var client = ClientWithActiveSessions(hasActiveSessions: true);
        var missionId = await CreateMissionAsync(client, "Con Sesiones");

        var update = new UpdateMissionRequest("Intento de cambio", "desc", "Medium", 30);
        var response = await client.PutAsJsonAsync($"/api/missions/{missionId}", update);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PATCH /api/missions/{id}/status (activar) ───────────────────────────────

    [Fact]
    public async Task ActivateMission_WithStages_TransitionsToActiveInPostgres()
    {
        var client = ClientWithActiveSessions(hasActiveSessions: false);
        var missionId = await CreateMissionAsync(client);

        // Activar exige que la misión tenga etapas: SessionService/MissionService
        // lo saben por la réplica StageCountLookup (sincronizada desde StageService).
        // La sembramos directamente, igual que SessionPersistenceTests siembra MissionLookup.
        using (var seedScope = fixture.Factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.StageCountLookup.Add(StageCountLookup.Create(missionId));
            await db.SaveChangesAsync();
        }

        var response = await client.PatchAsJsonAsync(
            $"/api/missions/{missionId}/status", new ChangeStatusRequest(Activate: true));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, because: await Body(response));

        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await dbContext.Missions.SingleAsync(m => m.Id == missionId);
        persisted.Status.Should().Be(MissionStatus.Active);
    }

    [Fact]
    public async Task ActivateMission_WithoutStages_ReturnsBadRequest()
    {
        // RB: no se puede activar una misión sin etapas (no hay StageCountLookup).
        var client = fixture.Factory.CreateClient();
        var missionId = await CreateMissionAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/missions/{missionId}/status", new ChangeStatusRequest(Activate: true));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

/// <summary>Doble de <see cref="ISessionServiceClient"/> para los tests de Mission.</summary>
file sealed class FakeSessionServiceClient(bool hasActiveSessions) : ISessionServiceClient
{
    public Task<bool> HasActiveSessionsAsync(Guid missionId, CancellationToken cancellationToken = default)
        => Task.FromResult(hasActiveSessions);
}
