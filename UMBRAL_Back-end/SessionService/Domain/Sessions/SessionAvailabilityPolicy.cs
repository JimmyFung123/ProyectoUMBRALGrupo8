namespace SessionService.Domain.Sessions;

/// <summary>
/// Domain Service que centraliza "¿se puede actuar sobre esta sesión ahora?".
/// Antes vivía como un `session.Status != SessionStatus.InProgress` idéntico,
/// repetido en AutoStartTeam, ForceAdvanceTeam, PenalizeTeam y ReleaseClue
/// (más la variante InProgress-o-Paused de BroadcastOperatorMessage). Cada
/// handler conserva su propio Error específico — esto solo centraliza la
/// condición, no reemplaza el resultado que devuelve cada uno.
/// </summary>
public static class SessionAvailabilityPolicy
{
    /// <summary>True cuando la sesión está en curso — la mayoría de las acciones
    /// de juego (avanzar equipo, penalizar, liberar pista) solo valen acá.</summary>
    public static bool IsInProgress(SessionStatus status) => status == SessionStatus.InProgress;

    /// <summary>True cuando la sesión acepta mensajes del operador a los
    /// participantes — InProgress o Paused (fuera de esa ventana, o no se
    /// conectaron todavía, o no deberían reaccionar a nada).</summary>
    public static bool AcceptsOperatorMessage(SessionStatus status) =>
        status == SessionStatus.InProgress || status == SessionStatus.Paused;
}
