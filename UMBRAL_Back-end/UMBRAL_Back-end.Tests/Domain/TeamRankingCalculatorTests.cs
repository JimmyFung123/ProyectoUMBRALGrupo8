namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using TeamService.Domain.Teams;
using Xunit;

/// <summary>
/// RB-08 sort + dense-rank algorithm, tested against plain in-memory Team
/// instances — no DbContext, no async, no EF change tracker. Same tie-break
/// scenarios as RankingProjectorTests, but exercising the pure logic directly.
/// </summary>
public class TeamRankingCalculatorTests
{
    private static Team NewTeam(string name, int score, DateTime? lastStageCompletedAt = null)
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create(name).Value);
        team.UpdateScore(score);
        if (lastStageCompletedAt.HasValue)
        {
            var prop = typeof(Team).GetProperty(nameof(Team.LastStageCompletedAt))!;
            prop.GetSetMethod(nonPublic: true)!.Invoke(team, new object?[] { lastStageCompletedAt.Value });
        }
        return team;
    }

    [Fact]
    public void Calculate_OrdersTeamsByScoreDescending()
    {
        var teams = new[] { NewTeam("Bajo", 50), NewTeam("Medio", 200), NewTeam("Alto", 350) };

        var result = TeamRankingCalculator.Calculate(teams);

        result.Select(r => r.Team.Name).Should().Equal("Alto", "Medio", "Bajo");
        result.Select(r => r.Position).Should().Equal(1, 2, 3);
        result.Select(r => r.Rank).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Calculate_OnEqualScore_TieBreaksByEarlierResolutionTime()
    {
        var earlier = new DateTime(2026, 5, 25, 10, 0, 0, DateTimeKind.Utc);
        var later = new DateTime(2026, 5, 25, 10, 5, 0, DateTimeKind.Utc);
        var teams = new[]
        {
            NewTeam("Equipo Tarde", 200, later),
            NewTeam("Equipo Temprano", 200, earlier),
        };

        var result = TeamRankingCalculator.Calculate(teams);

        result[0].Team.Name.Should().Be("Equipo Temprano");
        result[1].Team.Name.Should().Be("Equipo Tarde");
        result.Should().AllSatisfy(r => r.Rank.Should().Be(1)); // dense rank: empatados comparten Rank
    }

    [Fact]
    public void Calculate_OnEqualScore_TeamsWithoutResolutionTimeGoToBottom()
    {
        var teams = new[]
        {
            NewTeam("Sin tiempo", 100, null),
            NewTeam("Con tiempo", 100, new DateTime(2026, 5, 25, 9, 0, 0, DateTimeKind.Utc)),
        };

        var result = TeamRankingCalculator.Calculate(teams);

        result[0].Team.Name.Should().Be("Con tiempo");
        result[1].Team.Name.Should().Be("Sin tiempo");
    }

    [Fact]
    public void Calculate_DenseRanking_NextDistinctScoreGetsNextInteger()
    {
        // Dos equipos empatados en 200, uno en 100 → rank denso 1, 1, 3.
        var teams = new[]
        {
            NewTeam("Tied A", 200, new DateTime(2026, 5, 25, 9, 0, 0, DateTimeKind.Utc)),
            NewTeam("Tied B", 200, new DateTime(2026, 5, 25, 9, 5, 0, DateTimeKind.Utc)),
            NewTeam("Solo", 100),
        };

        var result = TeamRankingCalculator.Calculate(teams);

        result.Select(r => r.Rank).Should().Equal(1, 1, 3);
        result.Select(r => r.Position).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Calculate_EmptyCollection_ReturnsEmpty()
    {
        var result = TeamRankingCalculator.Calculate(Array.Empty<Team>());

        result.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_SingleTeam_GetsRankOnePositionOne()
    {
        var result = TeamRankingCalculator.Calculate(new[] { NewTeam("Solo", 42) });

        result.Should().ContainSingle();
        result[0].Rank.Should().Be(1);
        result[0].Position.Should().Be(1);
    }
}
