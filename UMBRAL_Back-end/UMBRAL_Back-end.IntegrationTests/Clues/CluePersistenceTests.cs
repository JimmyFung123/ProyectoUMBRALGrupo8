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

    [Fact]
    public async Task AddClue_ValidRequestThroughHttp_PersistsInRealPostgres()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CluesDbContext>();

        // AddClueCommandHandler resolves the stage via IStageLookupRepository (a local
        // projection kept in sync from StageService via StageAddedConsumer), so a
        // StageLookup row must exist before a clue can be added to it.
        var stageId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var stage = StageLookup.Create(stageId, missionId, "Trivia");
        dbContext.StagesLookup.Add(stage);
        await dbContext.SaveChangesAsync();

        var client = fixture.Factory.CreateClient();
        var request = new AddClueRequest(stageId, Order: 1, Content: $"Pista {Guid.NewGuid()}");

        var response = await client.PostAsJsonAsync("/api/clues", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // AddClueCommandHandler returns Result<Guid>, and ToHttpResult wraps it in
        // OkObjectResult(result.Value) — the body is a bare JSON Guid, not an object.
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

        // ClueConfiguration.Content is HasMaxLength(1000) -> varchar(1000). EF Core's
        // InMemory provider does not enforce column length, so this constraint only
        // surfaces against a real Postgres instance, same criterion used for
        // Team.InviteCode (varchar(10)).
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
}
