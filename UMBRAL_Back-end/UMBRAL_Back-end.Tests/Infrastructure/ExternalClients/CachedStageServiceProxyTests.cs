namespace UMBRAL_Back_end.Tests.Infrastructure.ExternalClients;

using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using SessionService.Application.Sessions;
using SessionService.Infrastructure.ExternalClients;
using Xunit;

/// <summary>
/// Pruebas del patrón Proxy. Se usa un <see cref="IStageServiceClient"/> interno simulado
/// con Moq (para contar cuántas veces se golpea al cliente real) y un <see cref="MemoryCache"/>
/// real (para ejercitar la caché de verdad, no un doble).
/// </summary>
public class CachedStageServiceProxyTests
{
    private readonly Mock<IStageServiceClient> _innerMock = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly CachedStageServiceProxy _proxy;

    public CachedStageServiceProxyTests()
    {
        _proxy = new CachedStageServiceProxy(_innerMock.Object, _cache);
    }

    private static StageWithOptionsInfo Stage(Guid id, string title = "Etapa") =>
        new(id, title, "Trivia", 1, 100, "¿?", new List<TriviaOptionInfo>());

    // ── Camino de caché ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStageWithOptions_FirstCall_DelegatesToInnerAndReturnsValue()
    {
        var stageId = Guid.NewGuid();
        var stage = Stage(stageId);
        _innerMock
            .Setup(c => c.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stage);

        var result = await _proxy.GetStageWithOptionsAsync(stageId, default);

        result.Should().BeSameAs(stage);
        _innerMock.Verify(
            c => c.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetStageWithOptions_SecondCallSameId_ServedFromCache_InnerCalledOnce()
    {
        var stageId = Guid.NewGuid();
        var stage = Stage(stageId);
        _innerMock
            .Setup(c => c.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stage);

        var first = await _proxy.GetStageWithOptionsAsync(stageId, default);
        var second = await _proxy.GetStageWithOptionsAsync(stageId, default);

        // Misma instancia las dos veces y el cliente real solo se golpeó una vez (hit en la 2ª).
        first.Should().BeSameAs(stage);
        second.Should().BeSameAs(stage);
        _innerMock.Verify(
            c => c.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetStageWithOptions_DifferentIds_CachedSeparately()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var stageA = Stage(idA, "A");
        var stageB = Stage(idB, "B");
        _innerMock.Setup(c => c.GetStageWithOptionsAsync(idA, It.IsAny<CancellationToken>())).ReturnsAsync(stageA);
        _innerMock.Setup(c => c.GetStageWithOptionsAsync(idB, It.IsAny<CancellationToken>())).ReturnsAsync(stageB);

        var a1 = await _proxy.GetStageWithOptionsAsync(idA, default);
        var b1 = await _proxy.GetStageWithOptionsAsync(idB, default);
        var a2 = await _proxy.GetStageWithOptionsAsync(idA, default);
        var b2 = await _proxy.GetStageWithOptionsAsync(idB, default);

        a1.Should().BeSameAs(stageA);
        a2.Should().BeSameAs(stageA);
        b1.Should().BeSameAs(stageB);
        b2.Should().BeSameAs(stageB);
        // Cada id tiene su propia entrada: una sola llamada real por id.
        _innerMock.Verify(c => c.GetStageWithOptionsAsync(idA, It.IsAny<CancellationToken>()), Times.Once);
        _innerMock.Verify(c => c.GetStageWithOptionsAsync(idB, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStageWithOptions_WhenInnerReturnsNull_NotCached_DelegatesAgain()
    {
        var stageId = Guid.NewGuid();
        var stage = Stage(stageId);
        // Primer intento falla (null, p. ej. error HTTP transitorio), el segundo trae la etapa.
        _innerMock
            .SetupSequence(c => c.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StageWithOptionsInfo?)null)
            .ReturnsAsync(stage);

        var first = await _proxy.GetStageWithOptionsAsync(stageId, default);
        var second = await _proxy.GetStageWithOptionsAsync(stageId, default);

        // El null no se cacheó: la 2ª llamada vuelve a golpear al cliente real y ya trae el valor.
        first.Should().BeNull();
        second.Should().BeSameAs(stage);
        _innerMock.Verify(
            c => c.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ── Pass-through ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStagesByMission_AlwaysDelegates_NotCached()
    {
        var missionId = Guid.NewGuid();
        IReadOnlyList<StageInfo> stages = new List<StageInfo> { new(Guid.NewGuid(), 1) };
        _innerMock
            .Setup(c => c.GetStagesByMissionAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stages);

        var first = await _proxy.GetStagesByMissionAsync(missionId, default);
        var second = await _proxy.GetStagesByMissionAsync(missionId, default);

        first.Should().BeSameAs(stages);
        second.Should().BeSameAs(stages);
        // La lista por misión no se cachea: ambas llamadas pasan directo al cliente real.
        _innerMock.Verify(
            c => c.GetStagesByMissionAsync(missionId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
