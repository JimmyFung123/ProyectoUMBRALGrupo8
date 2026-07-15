// This file touches types that live in StageService's aliased assembly (see the
// .csproj comment on the StageService ProjectReference for why the alias exists).
extern alias StageServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Stages;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using StageServiceAssembly::StageService.Adapter.Controllers;
using StageServiceAssembly::StageService.Application.Stages.Queries.GetStagesByMission;
using StageServiceAssembly::StageService.Domain.MissionLookup;
using StageServiceAssembly::StageService.Infrastructure.Persistence;
using Xunit;

[Collection(StageServiceCollection.Name)]
public class StagePersistenceTests(StageServicePostgresFixture fixture) : IAsyncLifetime
{
    // xUnit creates a new instance of this class per [Fact], so InitializeAsync runs before
    // EACH test — unlike the collection fixture, which is shared by the whole class. Resetting
    // here keeps tests isolated from rows left over by earlier tests in the same collection.
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> AddTriviaStageAsync(
        HttpClient client, Guid missionId, string? title = null, int order = 1)
    {
        var request = new AddStageRequest(
            missionId, title ?? $"Etapa {Guid.NewGuid()}", "Trivia", order, 10,
            "¿Pregunta de prueba?",
            [new OptionRequest("Correcta", true), new OptionRequest("Incorrecta", false)],
            null, null, null);
        var response = await client.PostAsJsonAsync("/api/stages", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>
    /// Siembra la réplica MissionLookup (sincronizada desde MissionService por eventos)
    /// para forzar el estado de la misión. Las mutaciones de etapa se bloquean cuando la
    /// misión está "Active" (StageMissionActivePolicy); si no hay fila, se trata como
    /// Inactive y se permiten — por eso los happy paths no necesitan sembrar nada.
    /// </summary>
    private async Task SeedMissionLookupAsync(Guid missionId, string status)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StagesDbContext>();
        db.MissionsLookup.Add(MissionLookup.Create(missionId, "Misión", status));
        await db.SaveChangesAsync();
    }

    private static async Task<string> Body(HttpResponseMessage r)
        => $"status inesperado; body: {await r.Content.ReadAsStringAsync()}";

    // ── Add + constraints + migración (base original) ───────────────────────────

    [Fact]
    public async Task AddStage_ValidRequestThroughHttp_PersistsInRealPostgres()
    {
        var client = fixture.Factory.CreateClient();
        var missionId = Guid.NewGuid();
        var request = new AddStageRequest(
            missionId,
            Title: $"Etapa {Guid.NewGuid()}",
            Type: "Trivia",
            Order: 1,
            BaseScore: 10,
            Question: "¿Cuál es la capital de Venezuela?",
            Options:
            [
                new OptionRequest("Caracas", true),
                new OptionRequest("Maracaibo", false)
            ],
            Latitude: null,
            Longitude: null,
            QrCode: null);

        var response = await client.PostAsJsonAsync("/api/stages", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stageId = await response.Content.ReadFromJsonAsync<Guid>();
        stageId.Should().NotBeEmpty();

        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StagesDbContext>();
        var persisted = await dbContext.Stages
            .Include(s => s.Options)
            .SingleAsync(s => s.Id == stageId);

        persisted.MissionId.Should().Be(missionId);
        persisted.Title.Should().Be(request.Title);
        persisted.Options.Should().HaveCount(2);
        persisted.Options.Should().ContainSingle(o => o.Text == "Caracas" && o.IsCorrect);
    }

    [Fact]
    public async Task SaveChanges_TriviaOptionWithNonExistentStageId_ThrowsDueToForeignKeyInPostgres()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StagesDbContext>();

        var orphanOptionId = Guid.NewGuid();
        var nonExistentStageId = Guid.NewGuid();

        var act = async () => await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "TriviaOptions" ("Id", "StageId", "Text", "IsCorrect")
             VALUES ({orphanOptionId}, {nonExistentStageId}, 'Opción huérfana', false)
             """);

        (await act.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be("23503");
    }

    [Fact]
    public async Task SaveChanges_TitleLongerThanColumnLimit_ThrowsDueToVarcharLengthInPostgres()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StagesDbContext>();

        var oversizedTitle = new string('A', 201);
        var id = Guid.NewGuid();

        var act = async () => await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "Stages"
                 ("Id", "MissionId", "Title", "Type", "Order", "BaseScore", "Question",
                  "Latitude", "Longitude", "QrCode", "AutoReleaseTimeMinutes", "AutoReleaseMaxAttempts", "CreatedAt")
             VALUES
                 ({id}, {Guid.NewGuid()}, {oversizedTitle}, 'Trivia', 1, 10, NULL,
                  NULL, NULL, NULL, NULL, NULL, {DateTime.UtcNow})
             """);

        (await act.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be("22001");
    }

    [Fact]
    public async Task Database_Migrate_AppliesAllMigrationsCleanlyAndIsIdempotent()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StagesDbContext>();

        var pendingBeforeReRun = await dbContext.Database.GetPendingMigrationsAsync();
        pendingBeforeReRun.Should().BeEmpty();

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        var allMigrations = dbContext.Database.GetMigrations();
        appliedMigrations.Should().BeEquivalentTo(allMigrations);
        allMigrations.Should().BeEquivalentTo(new[] { "20260523201927_FullStageModel" });

        var act = async () => await dbContext.Database.MigrateAsync();
        await act.Should().NotThrowAsync();

        (await dbContext.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }

    // ── GET /api/stages/{id} ────────────────────────────────────────────────────

    [Fact]
    public async Task GetStageById_ExistingStage_ReturnsStageWithOptions()
    {
        var client = fixture.Factory.CreateClient();
        var missionId = Guid.NewGuid();
        var stageId = await AddTriviaStageAsync(client, missionId, "Etapa Detalle");

        var response = await client.GetAsync($"/api/stages/{stageId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stage = await response.Content.ReadFromJsonAsync<StageDto>();
        stage.Should().NotBeNull();
        stage!.Id.Should().Be(stageId);
        stage.Title.Should().Be("Etapa Detalle");
        stage.Options.Should().Contain(o => o.Text == "Correcta" && o.IsCorrect);
    }

    [Fact]
    public async Task GetStageById_UnknownId_ReturnsNotFound()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/api/stages/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/stages?missionId ───────────────────────────────────────────────

    [Fact]
    public async Task GetStagesByMission_ReturnsStagesOfThatMissionOnly()
    {
        var client = fixture.Factory.CreateClient();
        var missionId = Guid.NewGuid();
        var otherMissionId = Guid.NewGuid();
        var first = await AddTriviaStageAsync(client, missionId, order: 1);
        var second = await AddTriviaStageAsync(client, missionId, order: 2);
        await AddTriviaStageAsync(client, otherMissionId, order: 1);

        var stages = await client.GetFromJsonAsync<List<StageDto>>($"/api/stages?missionId={missionId}");

        stages.Should().NotBeNull();
        stages!.Select(s => s.Id).Should().BeEquivalentTo(new[] { first, second });
    }

    // ── PUT /api/stages/{id} ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStage_ValidRequest_PersistsChangeInPostgres()
    {
        var client = fixture.Factory.CreateClient();
        var missionId = Guid.NewGuid();
        var stageId = await AddTriviaStageAsync(client, missionId, "Título Original");

        var update = new UpdateStageRequest(
            Title: "Título Actualizado", Order: 3, BaseScore: 50, Question: "¿Nueva pregunta?",
            Options: [new OptionRequest("Sí", true), new OptionRequest("No", false)],
            Latitude: null, Longitude: null, QrCode: null,
            AutoReleaseTimeMinutes: null, AutoReleaseMaxAttempts: null);
        var response = await client.PutAsJsonAsync($"/api/stages/{stageId}", update);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, because: await Body(response));

        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StagesDbContext>();
        var persisted = await dbContext.Stages.SingleAsync(s => s.Id == stageId);
        persisted.Title.Should().Be("Título Actualizado");
        persisted.BaseScore.Should().Be(50);
        persisted.Order.Should().Be(3);
    }

    [Fact]
    public async Task UpdateStage_UnknownId_ReturnsBadRequest()
    {
        var client = fixture.Factory.CreateClient();

        var update = new UpdateStageRequest(
            "X", 1, 10, "?", [new OptionRequest("a", true), new OptionRequest("b", false)],
            null, null, null, null, null);
        var response = await client.PutAsJsonAsync($"/api/stages/{Guid.NewGuid()}", update);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── DELETE /api/stages/{id} ─────────────────────────────────────────────────

    [Fact]
    public async Task RemoveStage_ExistingStage_DeletesFromPostgres()
    {
        var client = fixture.Factory.CreateClient();
        var missionId = Guid.NewGuid();
        var stageId = await AddTriviaStageAsync(client, missionId);

        var response = await client.DeleteAsync($"/api/stages/{stageId}?missionId={missionId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, because: await Body(response));

        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StagesDbContext>();
        (await dbContext.Stages.AnyAsync(s => s.Id == stageId)).Should().BeFalse();
    }

    // ── PATCH /api/stages/{id}/auto-release ─────────────────────────────────────

    [Fact]
    public async Task SetAutoRelease_PersistsRuleInPostgres()
    {
        var client = fixture.Factory.CreateClient();
        var missionId = Guid.NewGuid();
        var stageId = await AddTriviaStageAsync(client, missionId);

        var response = await client.PatchAsJsonAsync(
            $"/api/stages/{stageId}/auto-release", new AutoReleaseRequest(TimeMinutes: null, MaxAttempts: 3));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, because: await Body(response));

        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StagesDbContext>();
        var persisted = await dbContext.Stages.SingleAsync(s => s.Id == stageId);
        persisted.AutoReleaseMaxAttempts.Should().Be(3);
    }

    // ── Regla cross-service: misión activa bloquea mutar etapas ─────────────────

    [Fact]
    public async Task AddStage_WhenMissionIsActive_IsRejected()
    {
        // StageMissionActivePolicy: con la misión "Active" en la réplica local, no se
        // puede agregar/editar/borrar etapas.
        var client = fixture.Factory.CreateClient();
        var missionId = Guid.NewGuid();
        await SeedMissionLookupAsync(missionId, "Active");

        var request = new AddStageRequest(
            missionId, "Etapa bloqueada", "Trivia", 1, 10, "¿?",
            [new OptionRequest("a", true), new OptionRequest("b", false)], null, null, null);
        var response = await client.PostAsJsonAsync("/api/stages", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
