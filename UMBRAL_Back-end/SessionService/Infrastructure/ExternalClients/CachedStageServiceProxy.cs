namespace SessionService.Infrastructure.ExternalClients;

using Microsoft.Extensions.Caching.Memory;
using SessionService.Application.Sessions;

/// <summary>
/// Patrón Proxy (proxy de caché). Envuelve al <see cref="IStageServiceClient"/> real
/// (<see cref="StageServiceClient"/>) y memoiza <see cref="GetStageWithOptionsAsync"/>
/// en un <see cref="IMemoryCache"/> compartido.
///
/// El consumidor (la fachada, los handlers de evidencia, GetReleasedClues y el endpoint
/// de estructura del Composite) sigue dependiendo solo de <see cref="IStageServiceClient"/>:
/// no sabe que entre medio hay una caché. Durante una sesión en vivo el detalle de una
/// etapa (título, opciones, puntaje base, coordenadas) es inmutable, así que la misma
/// etapa se pide una y otra vez —en cada respuesta de trivia, en cada poll del
/// participante y N veces en el bucle del Composite—. Cachearla evita golpear StageService
/// por HTTP en cada una de esas llamadas.
///
/// Notas de diseño:
///   • Solo se cachea <see cref="GetStageWithOptionsAsync"/>; <see cref="GetStagesByMissionAsync"/>
///     pasa directo al cliente real (la lista por misión cambia con menos frecuencia y no es
///     el cuello de botella que motivó el proxy).
///   • Expiración absoluta corta (<see cref="CacheTtl"/>): StageService es otro microservicio
///     y SessionService no consume eventos de cambio de etapa, así que la invalidación es por
///     caducidad acotada en vez de explícita. 30 s es invisible dentro de una partida y a la
///     vez recoge cualquier edición de etapa hecha entre sesiones.
///   • No se cachean resultados <c>null</c>: <see cref="StageServiceClient"/> devuelve
///     <c>null</c> ante un fallo HTTP, y fijar ese fallo transitorio en caché lo volvería
///     pegajoso. Solo se memoiza una respuesta válida.
/// </summary>
public class CachedStageServiceProxy : IStageServiceClient
{
    private readonly IStageServiceClient _inner;
    private readonly IMemoryCache _cache;

    /// <summary>Tiempo de vida de cada etapa cacheada (expiración absoluta).</summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private const string CacheKeyPrefix = "stage-with-options:";

    public CachedStageServiceProxy(IStageServiceClient inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    /// <summary>Pass-through: la lista de etapas por misión no se cachea.</summary>
    public Task<IReadOnlyList<StageInfo>> GetStagesByMissionAsync(Guid missionId, CancellationToken ct)
        => _inner.GetStagesByMissionAsync(missionId, ct);

    /// <summary>
    /// Devuelve el detalle de la etapa desde la caché si está presente; si no, lo pide al
    /// cliente real y memoiza el resultado (solo cuando no es <c>null</c>).
    /// </summary>
    public async Task<StageWithOptionsInfo?> GetStageWithOptionsAsync(Guid stageId, CancellationToken ct)
    {
        var key = CacheKeyPrefix + stageId;
        if (_cache.TryGetValue(key, out StageWithOptionsInfo? cached))
            return cached;

        var stage = await _inner.GetStageWithOptionsAsync(stageId, ct);
        if (stage is not null)
            _cache.Set(key, stage, CacheTtl);

        return stage;
    }
}
