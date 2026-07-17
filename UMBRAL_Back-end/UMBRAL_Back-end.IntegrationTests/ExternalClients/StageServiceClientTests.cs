// StageServiceClient vive en el ensamblado de SessionService (extern alias, ver el .csproj).
extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.ExternalClients;

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SessionServiceAssembly::SessionService.Infrastructure.ExternalClients;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using Xunit;

/// <summary>
/// Ejercita el <see cref="StageServiceClient"/> real contra un UpstreamJsonStub: cubre el
/// mapeo del feed de etapas y del detalle con opciones, más los defaults seguros (lista
/// vacía / null) de las ramas no-2xx. Es la capa Infrastructure de salida HTTP que los
/// tests de controller no ejercitan al sustituir el cliente por un fake.
/// </summary>
public class StageServiceClientTests
{
    [Fact]
    public async Task GetStagesByMission_MapsItems_OnSuccess()
    {
        var stageId = Guid.NewGuid();
        await using var stub = await StubHttp.Returning($$"""
            [{"id":"{{stageId}}","order":1,"type":"TreasureHunt","autoReleaseTimeMinutes":5}]
            """);
        var result = await new StageServiceClient(StubHttp.ClientTo(stub))
            .GetStagesByMissionAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(stageId);
        result[0].Order.Should().Be(1);
        result[0].Type.Should().Be("TreasureHunt");
        result[0].AutoReleaseTimeMinutes.Should().Be(5);
    }

    [Fact]
    public async Task GetStagesByMission_DefaultsTypeToTrivia_WhenUpstreamOmitsIt()
    {
        await using var stub = await StubHttp.Returning("""[{"id":"11111111-1111-1111-1111-111111111111","order":2}]""");
        var result = await new StageServiceClient(StubHttp.ClientTo(stub))
            .GetStagesByMissionAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Type.Should().Be("Trivia");
    }

    [Fact]
    public async Task GetStagesByMission_ReturnsEmpty_OnNonSuccessStatus()
    {
        await using var stub = await StubHttp.Returning("[]", statusCode: 500);
        var result = await new StageServiceClient(StubHttp.ClientTo(stub))
            .GetStagesByMissionAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStagesByMission_ReturnsEmpty_WhenBodyIsNotValidJson()
    {
        await using var stub = await StubHttp.Returning("no-soy-json");
        var result = await new StageServiceClient(StubHttp.ClientTo(stub))
            .GetStagesByMissionAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStageWithOptions_MapsStageAndOptions_OnSuccess()
    {
        var stageId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        await using var stub = await StubHttp.Returning($$"""
            {"id":"{{stageId}}","title":"Etapa 1","type":"Trivia","order":1,"baseScore":100,
             "question":"¿Capital?","options":[{"id":"{{optionId}}","text":"Caracas","isCorrect":true}],
             "latitude":null,"longitude":null,"qrCode":null,"autoReleaseMaxAttempts":3}
            """);
        var result = await new StageServiceClient(StubHttp.ClientTo(stub))
            .GetStageWithOptionsAsync(stageId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(stageId);
        result.Title.Should().Be("Etapa 1");
        result.BaseScore.Should().Be(100);
        result.Options.Should().ContainSingle();
        result.Options[0].Id.Should().Be(optionId);
        result.Options[0].IsCorrect.Should().BeTrue();
        result.AutoReleaseMaxAttempts.Should().Be(3);
    }

    [Fact]
    public async Task GetStageWithOptions_ReturnsNull_OnNonSuccessStatus()
    {
        await using var stub = await StubHttp.Returning("{}", statusCode: 404);
        var result = await new StageServiceClient(StubHttp.ClientTo(stub))
            .GetStageWithOptionsAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }
}
