// ClueServiceClient vive en el ensamblado de SessionService (extern alias, ver el .csproj).
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
/// Ejercita el <see cref="ClueServiceClient"/> real contra un UpstreamJsonStub: cubre el
/// mapeo y el orden por <c>Order</c> de las pistas, más la lista vacía de la rama no-2xx.
/// </summary>
public class ClueServiceClientTests
{
    [Fact]
    public async Task GetCluesByStage_MapsAndOrdersByOrder_OnSuccess()
    {
        var clue1 = Guid.NewGuid();
        var clue2 = Guid.NewGuid();
        // Se devuelven desordenadas a propósito: el cliente debe ordenarlas por Order.
        await using var stub = await StubHttp.Returning($$"""
            [{"id":"{{clue2}}","order":2,"content":"Segunda","latitude":null,"longitude":null,"radiusMeters":null,"autoReleaseAfterMinutes":null},
             {"id":"{{clue1}}","order":1,"content":"Primera","latitude":10.5,"longitude":-66.9,"radiusMeters":50,"autoReleaseAfterMinutes":3}]
            """);
        var result = await new ClueServiceClient(StubHttp.ClientTo(stub))
            .GetCluesByStageAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Order.Should().Be(1);
        result[0].Id.Should().Be(clue1);
        result[0].RadiusMeters.Should().Be(50);
        result[1].Order.Should().Be(2);
    }

    [Fact]
    public async Task GetCluesByStage_ReturnsEmpty_OnNonSuccessStatus()
    {
        await using var stub = await StubHttp.Returning("[]", statusCode: 500);
        var result = await new ClueServiceClient(StubHttp.ClientTo(stub))
            .GetCluesByStageAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCluesByStage_ReturnsEmpty_WhenBodyIsNotValidJson()
    {
        await using var stub = await StubHttp.Returning("no-soy-json");
        var result = await new ClueServiceClient(StubHttp.ClientTo(stub))
            .GetCluesByStageAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
