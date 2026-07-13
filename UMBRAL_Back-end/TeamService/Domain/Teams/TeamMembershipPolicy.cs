namespace TeamService.Domain.Teams;

/// <summary>
/// Domain Service que centraliza las reglas de membresía de un equipo:
///   * RB-18: un equipo necesita un mínimo de miembros para poder competir.
///   * Capacidad máxima: un equipo no admite más miembros una vez lleno
///     (materializa el error <see cref="TeamErrors.TeamFull"/>).
///   * Cuándo un equipo queda vacío tras un abandono y debe eliminarse.
///
/// Lógica de dominio pura, sin I/O: recibe el conteo de miembros ya cargado y
/// expone las comparaciones como reglas con nombre. La entidad <see cref="Team"/>
/// delega en esta policy en <c>Join()</c> y <c>Leave()</c>, de modo que los
/// umbrales dejan de ser literales dispersos.
/// </summary>
public static class TeamMembershipPolicy
{
    /// <summary>RB-18: mínimo de miembros para que el equipo pueda arrancar la sesión.</summary>
    public const int MinimumMembers = 2;

    /// <summary>Capacidad máxima de un equipo (número de miembros que admite).</summary>
    public const int MaximumMembers = 6;

    /// <summary>True cuando el equipo aún tiene cupo para un miembro más.</summary>
    public static bool CanJoin(int currentMemberCount)
        => currentMemberCount < MaximumMembers;

    /// <summary>True cuando el equipo alcanza el mínimo para competir (RB-18).</summary>
    public static bool MeetsStartRequirement(int memberCount)
        => memberCount >= MinimumMembers;

    /// <summary>True cuando, tras un abandono, el equipo se quedó sin miembros
    /// (el caller debe eliminarlo; el equipo no puede borrar su propia fila).</summary>
    public static bool IsEmptyAfterLeave(int memberCountAfterLeave)
        => memberCountAfterLeave <= 0;
}
