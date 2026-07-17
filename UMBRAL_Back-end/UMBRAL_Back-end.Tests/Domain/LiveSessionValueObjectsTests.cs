namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using Xunit;
using SessionVo = SessionService.Domain.Sessions;
using TeamVo = TeamService.Domain.Teams;

/// <summary>
/// Live Session value objects (v2): SessionCode, TeamName, TeamCode.
/// </summary>
public class LiveSessionValueObjectsTests
{
    // ── SessionCode ──────────────────────────────────────────────────────────

    [Fact]
    public void SessionCode_Generate_HasConfiguredLength()
        => SessionVo.SessionCode.Generate().Value.Length.Should().Be(SessionVo.SessionCode.Length);

    [Fact]
    public void SessionCode_Generate_UsesAllowedAlphabetOnly()
    {
        var code = SessionVo.SessionCode.Generate().Value;
        code.Should().MatchRegex("^[A-Z0-9]+$");
    }

    // ── TeamCode (invite code) ───────────────────────────────────────────────

    [Fact]
    public void TeamCode_Generate_HasConfiguredLength()
        => TeamVo.TeamCode.Generate().Value.Length.Should().Be(TeamVo.TeamCode.Length);

    [Fact]
    public void TeamCode_Generate_ExcludesAmbiguousCharacters()
    {
        var code = TeamVo.TeamCode.Generate().Value;
        // No 0/O/1/I to keep codes readable when shared aloud.
        code.Should().MatchRegex("^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]+$");
    }

    // ── TeamName ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TeamName_WhenBlank_ReturnsInvalidTeamName(string? raw)
    {
        var result = TeamVo.TeamName.Create(raw);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamVo.TeamErrors.InvalidTeamName);
    }

    [Fact]
    public void TeamName_TrimsValue()
        => TeamVo.TeamName.Create("  Equipo Alfa ").Value.Value.Should().Be("Equipo Alfa");

    [Fact]
    public void TeamName_WhenTooLong_ReturnsInvalidTeamName()
    {
        var tooLong = new string('a', TeamVo.TeamName.MaxLength + 1);
        TeamVo.TeamName.Create(tooLong).Error.Should().Be(TeamVo.TeamErrors.InvalidTeamName);
    }
}
