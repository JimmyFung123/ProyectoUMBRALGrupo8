// This file touches types that live in ClueService's aliased assembly (see the
// .csproj comment on the ClueService ProjectReference for why the alias exists).
extern alias ClueServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Clues;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using ClueServiceAssembly::ClueService.Adapter.Controllers;
using ClueServiceAssembly::ClueService.Application.Clues.Queries.GetCluesByStage;
using ClueServiceAssembly::ClueService.Domain.StageLookup;
using ClueServiceAssembly::ClueService.Infrastructure.Persistence;
using Xunit;

[Collection(ClueServiceCollection.Name)]
public class CluePersistenceTests(ClueServicePostgresFixture fixture) : IAsyncLifetime
{
    // xUnit creates a new instance of this class per [Fact], so InitializeAsync runs before
    // EACH test — unlike the collection fixture, which is shared by the whole class. Resetting
    // here keeps tests isolated from rows left over by earlier tests in the same collection.
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Siembra la réplica StageLookup (sincronizada desde StageService por eventos).
    /// AddClue resuelve la etapa contra esta proyección, así que sin fila devuelve
    /// Clue.StageNotFound (404) — por eso los happy paths deben sembrarla primero.
    /// </summary>
    private async Task SeedStageAsync(Guid stageId, Guid missionId, string type = "Trivia")
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CluesDbContext>();
        db.StagesLookup.Add(StageLookup.Create(stageId, missionId, type));
        await db.SaveChangesAsync();
    }

    private async Task<Guid> AddClueAsync(HttpClient client, Guid stageId, int order, string content)
    {
        var response = await client.PostAsJsonAsync(
            "/api/clues", new AddClueRequest(stageId, Order: order, Content: content));
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<string> Body(HttpResponseMessage r)
        => $"status inesperado; body: {await r.Content.ReadAsStringAsync()}";

    // ── Add + constraints + migración (base original) ───────────────────────────

    [Fact]
    public async Task AddClue_ValidRequestThroughHttp_PersistsInRealPostgres()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CluesDbContext>();

        var stageId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var stage = StageLookup.Create(stageId, missionId, "Trivia");
        dbContext.StagesLookup.Add(stage);
        await dbContext.SaveChangesAsync();

        var client = fixture.Factory.CreateClient();
        var request = new AddClueRequest(stageId, Order: 1, Content: $"Pista {Guid.NewGuid()}");

        var response = await client.PostAsJsonAsync("/api/clues", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var clueId = await response.Content.ReadFromJsonAsync<Guid>();
        clueId.Should().NotBeEmpty();

        var persisted = await dbContext.Clues.SingleAsync(c => c.Id == clueId);
        persisted.StageId.Should().Be(stageId);
        persisted.MissionId.Should().Be(missionId);
        persisted.Content.Should().Be(request.Content);
        persisted.Order.Should().Be(1);
        persisted.StageType.Should().Be("Trivia");
    }

    [Fact]
    public async Task SaveChanges_ContentLongerThanColumnLimit_ThrowsDueToVarcharLengthInPostgres()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CluesDbContext>();

        var oversizedContent = new string('A', 1001);
        var id = Guid.NewGuid();

        var act = async () => await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "Clues"
                 ("Id", "StageId", "MissionId", "Content", "Order", "CreatedAt",
                  "Latitude", "Longitude", "RadiusMeters", "StageType", "AutoReleaseAfterMinutes")
             VALUES
                 ({id}, {Guid.NewGuid()}, {Guid.NewGuid()}, {oversizedContent}, 1, {DateTime.UtcNow},
                  NULL, NULL, NULL, 'Trivia', NULL)
             """);

        (await act.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be("22001");
    }

    [Fact]
    public async Task Database_Migrate_AppliesAllMigrationsCleanlyAndIsIdempotent()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CluesDbContext>();

        var pendingBeforeReRun = await dbContext.Database.GetPendingMigrationsAsync();
        pendingBeforeReRun.Should().BeEmpty();

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        var allMigrations = dbContext.Database.GetMigrations();
        appliedMigrations.Should().BeEquivalentTo(allMigrations);
        allMigrations.Should().BeEquivalentTo(new[]
        {
            "20260523185327_Initial",
            "20260524074423_AddAutoReleaseAfterMinutes",
            "20260525090000_AddGeoClueData"
        });

        var act = async () => await dbContext.Database.MigrateAsync();
        await act.Should().NotThrowAsync();

        (await dbContext.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }

    // ── GET /api/clues?stageId ──────────────────────────────────────────────────

    [Fact]
    public async Task GetCluesByStage_ReturnsCluesOfThatStage()
    {
        var stageId = Guid.NewGuid();
        await SeedStageAsync(stageId, Guid.NewGuid());
        var client = fixture.Factory.CreateClient();
        var first = await AddClueAsync(client, stageId, 1, "Pista uno");
        var second = await AddClueAsync(client, stageId, 2, "Pista dos");

        var clues = await client.GetFromJsonAsync<List<ClueDto>>($"/api/clues?stageId={stageId}");

        clues.Should().NotBeNull();
        clues!.Select(c => c.Id).Should().BeEquivalentTo(new[] { first, second });
    }

    // ── PUT /api/clues/{id} ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateClue_ValidRequest_PersistsChangeInPostgres()
    {
        var stageId = Guid.NewGuid();
        await SeedStageAsync(stageId, Guid.NewGuid());
        var client = fixture.Factory.CreateClient();
        var clueId = await AddClueAsync(client, stageId, 1, "Contenido original");

        var response = await client.PutAsJsonAsync(
            $"/api/clues/{clueId}", new UpdateClueRequest(Order: 5, Content: "Contenido actualizado"));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, because: await Body(response));

        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CluesDbContext>();
        var persisted = await dbContext.Clues.SingleAsync(c => c.Id == clueId);
        persisted.Content.Should().Be("Contenido actualizado");
        persisted.Order.Should().Be(5);
    }

    [Fact]
    public async Task UpdateClue_UnknownId_ReturnsNotFound()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/clues/{Guid.NewGuid()}", new UpdateClueRequest(Order: 1, Content: "x"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/clues/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task RemoveClue_ExistingClue_DeletesFromPostgres()
    {
        var stageId = Guid.NewGuid();
        await SeedStageAsync(stageId, Guid.NewGuid());
        var client = fixture.Factory.CreateClient();
        var clueId = await AddClueAsync(client, stageId, 1, "A borrar");

        var response = await client.DeleteAsync($"/api/clues/{clueId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, because: await Body(response));

        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CluesDbContext>();
        (await dbContext.Clues.AnyAsync(c => c.Id == clueId)).Should().BeFalse();
    }

    // ── Regla: no se puede agregar pista a una etapa inexistente ────────────────

    [Fact]
    public async Task AddClue_WhenStageDoesNotExist_ReturnsNotFound()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/clues", new AddClueRequest(Guid.NewGuid(), Order: 1, Content: "Sin etapa"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
