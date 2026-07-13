namespace TeamService.Domain.Teams;

/// <summary>
/// Resultado del cálculo de ranking para un equipo: su posición absoluta
/// (siempre 1..N) y su Rank denso (equipos empatados comparten Rank).
/// </summary>
public record TeamRankResult(Team Team, int Rank, int Position);

/// <summary>
/// Domain Service que calcula el ranking de un conjunto de equipos (RB-08):
/// orden Score desc → LastStageCompletedAt asc (nulls last) → Name, con
/// ranking denso — equipos empatados comparten Rank, Position siempre es 1..N.
///
/// Lógica pura, sin I/O: extraída de RankingProjector (que sí hace I/O de EF Core
/// para leer los equipos y persistir la proyección) para que el algoritmo de
/// desempate — la parte más fácil de romper sin darse cuenta — sea testeable con
/// listas en memoria, sin EF InMemory ni mocks de DbContext.
/// </summary>
public static class TeamRankingCalculator
{
    public static IReadOnlyList<TeamRankResult> Calculate(IEnumerable<Team> teams)
    {
        var sorted = teams
            .OrderByDescending(t => t.Score)
            .ThenBy(t => t.LastStageCompletedAt ?? DateTime.MaxValue)
            .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var results = new List<TeamRankResult>(sorted.Count);
        int rank = 1;

        for (int i = 0; i < sorted.Count; i++)
        {
            if (i > 0 && sorted[i].Score < sorted[i - 1].Score)
                rank = i + 1;

            results.Add(new TeamRankResult(sorted[i], rank, Position: i + 1));
        }

        return results;
    }
}
