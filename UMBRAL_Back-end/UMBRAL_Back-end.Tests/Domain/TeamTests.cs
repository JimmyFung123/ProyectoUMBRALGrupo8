namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using TeamService.Domain.Teams;
using Xunit;

public class TeamTests
{
    // ── Creación ──────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidData_SetsAllDefaultsCorrectly()
    {
        var sessionId = Guid.NewGuid();

        var team = Team.Create(sessionId, "Equipo Alfa");

        team.Id.Should().NotBeEmpty();
        team.SessionId.Should().Be(sessionId);
        team.Name.Should().Be("Equipo Alfa");
        team.IsConnected.Should().BeFalse();
        team.Score.Should().Be(0);
        team.CurrentStageOrder.Should().Be(0);
        team.CluesReceivedCurrentStage.Should().Be(0);
        team.TotalCluesReceived.Should().Be(0);
    }

    [Fact]
    public void Create_AssignsUniqueIds()
    {
        var sessionId = Guid.NewGuid();

        var team1 = Team.Create(sessionId, "Equipo Alfa");
        var team2 = Team.Create(sessionId, "Equipo Beta");

        team1.Id.Should().NotBe(team2.Id);
    }

    [Theory]
    [InlineData("  Equipo Alfa  ", "Equipo Alfa")]
    [InlineData("Equipo Beta", "Equipo Beta")]
    [InlineData("  Los Invictos  ", "Los Invictos")]
    public void Create_TrimsWhitespaceFromName(string input, string expected)
    {
        var team = Team.Create(Guid.NewGuid(), input);

        team.Name.Should().Be(expected);
    }

    // ── Conexión ──────────────────────────────────────────────────────────────

    [Fact]
    public void SetConnected_ToTrue_MarkAsConnected()
    {
        var team = Team.Create(Guid.NewGuid(), "Equipo Alfa");

        team.SetConnected(true);

        team.IsConnected.Should().BeTrue();
    }

    [Fact]
    public void SetConnected_ToFalse_MarkAsDisconnected()
    {
        var team = Team.Create(Guid.NewGuid(), "Equipo Alfa");
        team.SetConnected(true);

        team.SetConnected(false);

        team.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void SetConnected_CanToggleMultipleTimes()
    {
        var team = Team.Create(Guid.NewGuid(), "Equipo Alfa");

        team.SetConnected(true);
        team.IsConnected.Should().BeTrue();

        team.SetConnected(false);
        team.IsConnected.Should().BeFalse();

        team.SetConnected(true);
        team.IsConnected.Should().BeTrue();
    }

    // ── Progreso ──────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateProgress_SetsAllProgressFields()
    {
        var team = Team.Create(Guid.NewGuid(), "Equipo Alfa");

        team.UpdateProgress(stageOrder: 3, cluesCurrentStage: 2, totalClues: 5);

        team.CurrentStageOrder.Should().Be(3);
        team.CluesReceivedCurrentStage.Should().Be(2);
        team.TotalCluesReceived.Should().Be(5);
    }

    [Fact]
    public void UpdateProgress_OverridesPreviousValues()
    {
        var team = Team.Create(Guid.NewGuid(), "Equipo Alfa");
        team.UpdateProgress(stageOrder: 1, cluesCurrentStage: 1, totalClues: 1);

        team.UpdateProgress(stageOrder: 4, cluesCurrentStage: 0, totalClues: 3);

        team.CurrentStageOrder.Should().Be(4);
        team.CluesReceivedCurrentStage.Should().Be(0);
        team.TotalCluesReceived.Should().Be(3);
    }

    // ── Score ─────────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateScore_SetsScore()
    {
        var team = Team.Create(Guid.NewGuid(), "Equipo Alfa");

        team.UpdateScore(350);

        team.Score.Should().Be(350);
    }

    [Fact]
    public void UpdateScore_CanBeUpdatedMultipleTimes()
    {
        var team = Team.Create(Guid.NewGuid(), "Equipo Alfa");

        team.UpdateScore(100);
        team.UpdateScore(250);
        team.UpdateScore(400);

        team.Score.Should().Be(400);
    }

    [Fact]
    public void UpdateScore_AcceptsZero()
    {
        var team = Team.Create(Guid.NewGuid(), "Equipo Alfa");
        team.UpdateScore(300);

        team.UpdateScore(0);

        team.Score.Should().Be(0);
    }
}
