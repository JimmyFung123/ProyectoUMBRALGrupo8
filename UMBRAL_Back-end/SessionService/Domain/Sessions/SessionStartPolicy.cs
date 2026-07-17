namespace SessionService.Domain.Sessions;

/// <summary>
/// Domain Service que encapsula las precondiciones de negocio para arrancar una
/// sesión (HU-12):
///   * RB-02: debe haber al menos un equipo inscrito.
///   * RB-18: todo equipo inscrito debe tener el mínimo de miembros.
///
/// Lógica de dominio pura, sin I/O: recibe datos ya cargados (conteos) para que
/// tanto el handler como los adaptadores de infraestructura decidan *cuándo*
/// consultar a TeamService, mientras que las *reglas* (los umbrales y sus
/// comparaciones) viven aquí y son trivialmente testeables. Los umbrales quedan
/// como constantes con nombre, no como literales dispersos por el código.
/// </summary>
public static class SessionStartPolicy
{
    /// <summary>RB-02: número mínimo de equipos inscritos para poder arrancar.</summary>
    public const int MinimumEnrolledTeams = 1;

    /// <summary>RB-18: número mínimo de miembros que debe tener cada equipo.</summary>
    public const int MinimumMembersPerTeam = 2;

    /// <summary>True cuando hay suficientes equipos inscritos (RB-02).</summary>
    public static bool MeetsTeamRequirement(int enrolledTeamCount)
        => enrolledTeamCount >= MinimumEnrolledTeams;

    /// <summary>True cuando un equipo alcanza el mínimo de miembros (RB-18).</summary>
    public static bool MeetsMinimumMembers(int memberCount)
        => memberCount >= MinimumMembersPerTeam;

    /// <summary>True cuando todos los equipos alcanzan el mínimo de miembros (RB-18).</summary>
    public static bool AllTeamsMeetMinimumMembers(IEnumerable<int> memberCounts)
        => memberCounts.All(MeetsMinimumMembers);
}
