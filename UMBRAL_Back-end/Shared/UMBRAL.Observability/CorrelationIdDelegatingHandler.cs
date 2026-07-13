namespace UMBRAL.Observability;

/// <summary>
/// <see cref="DelegatingHandler"/> que adjunta el correlation id actual a cada
/// petición HTTP saliente de los clientes tipados (TeamService, StageService,
/// ClueService, MissionService, clientes de sync-health...). Así, cuando un
/// servicio llama a otro por HTTP, ambos comparten el mismo id en sus logs.
///
/// Se aplica a todos los HttpClient vía <c>ConfigureHttpClientDefaults</c> en
/// <see cref="UmbralCorrelationExtensions.AddUmbralCorrelationId"/>.
/// </summary>
public sealed class CorrelationIdDelegatingHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIdContext.Current;

        // No pisar un id ya presente (p.ej. si el llamador lo puso explícitamente).
        if (!string.IsNullOrWhiteSpace(correlationId)
            && !request.Headers.Contains(CorrelationConstants.HeaderName))
        {
            request.Headers.TryAddWithoutValidation(CorrelationConstants.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
