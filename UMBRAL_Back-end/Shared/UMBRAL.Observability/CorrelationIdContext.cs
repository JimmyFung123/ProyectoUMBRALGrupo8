namespace UMBRAL.Observability;

/// <summary>
/// Portador ambiental del correlation id de la operación en curso. Usa
/// <see cref="AsyncLocal{T}"/> para que el valor fluya por la cadena async tanto
/// en una petición HTTP (lo fija el middleware) como al consumir un mensaje de
/// MassTransit (lo fija el filtro de consumo), sin tener que pasarlo a mano.
///
/// Lo leen el <c>CorrelationIdDelegatingHandler</c> (para reenviarlo en llamadas
/// HTTP salientes) y los filtros de envío/publicación de MassTransit (para ponerlo
/// como cabecera del mensaje), cerrando así la propagación extremo a extremo.
/// </summary>
public static class CorrelationIdContext
{
    private static readonly AsyncLocal<string?> _current = new();

    /// <summary>Correlation id de la operación actual, o null si aún no se fijó.</summary>
    public static string? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    /// <summary>Devuelve el id actual o genera y fija uno nuevo si no existe.</summary>
    public static string GetOrCreate()
        => Current ??= Guid.NewGuid().ToString();
}
