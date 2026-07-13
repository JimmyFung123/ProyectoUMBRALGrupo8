namespace UMBRAL.Observability;

/// <summary>
/// Constantes compartidas para la trazabilidad por correlación entre microservicios.
/// El mismo nombre de cabecera se usa en HTTP (request/response) y como cabecera de
/// mensaje en MassTransit, de modo que un único id sigue una operación a través de
/// todos los servicios y aparece en los logs de cada uno.
/// </summary>
public static class CorrelationConstants
{
    /// <summary>Cabecera HTTP y de mensajería que transporta el correlation id.</summary>
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>Clave con la que el correlation id se expone en el scope de logging.</summary>
    public const string LogPropertyName = "CorrelationId";
}
