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
using StageServiceAssembly::StageService.Infrastructure.Persistence;
using Xunit;

[Collection(StageServiceCollection.Name)]
public class StagePersistenceTests(StageServicePostgresFixture fixture)
{
    [Fact]
    public async Task AddStage_ValidRequestThroughHttp_PersistsInRealPostgres()
    {
        // AddStageCommandHandler only blocks mutation when the local MissionLookup
        // projection says the mission IS active (StageMissionActivePolicy.BlocksStageMutation);
        // an absent lookup row (self-heals when MissionCreated/Activated arrives) is treated as
        // Inactive, so — unlike ClueService's stage-must-exist requirement — no seed row is needed here.
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

        // AddStageCommandHandler returns Result<Guid>, and the controller does
        // Ok(result.Value) directly — the body is a bare JSON Guid, not an object.
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

        // TriviaOptions.StageId is a real FK to Stages.Id (FK_TriviaOptions_Stages_StageId,
        // see the FullStageModel migration). EF Core's InMemory provider does not enforce
        // foreign keys against a real relational schema, so this constraint only surfaces
        // against a real Postgres instance.
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

        // StageConfiguration.Title is HasMaxLength(200) -> varchar(200), not enforced by
        // EF Core's InMemory provider — same criterion used for Clue.Content (varchar(1000))
        // and Team.InviteCode (varchar(10)).
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
}
