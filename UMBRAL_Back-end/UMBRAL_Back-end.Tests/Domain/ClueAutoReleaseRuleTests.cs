namespace UMBRAL_Back_end.Tests.Domain;

using ClueService.Domain.Clues;
using FluentAssertions;
using Xunit;

public class ClueAutoReleaseRuleTests
{
    [Fact]
    public void Create_WithAutoReleaseMinutes_SetsProperty()
    {
        var result = Clue.Create(Guid.NewGuid(), Guid.NewGuid(), "Find the fountain", 1, autoReleaseAfterMinutes: 5);
        result.IsSuccess.Should().BeTrue();
        result.Value.AutoReleaseAfterMinutes.Should().Be(5);
    }

    [Fact]
    public void Create_WithoutAutoReleaseMinutes_IsNull()
    {
        var result = Clue.Create(Guid.NewGuid(), Guid.NewGuid(), "Find the fountain", 1);
        result.IsSuccess.Should().BeTrue();
        result.Value.AutoReleaseAfterMinutes.Should().BeNull();
    }

    [Fact]
    public void SetAutoRelease_UpdatesProperty()
    {
        var clue = Clue.Create(Guid.NewGuid(), Guid.NewGuid(), "Find the fountain", 1).Value;
        clue.SetAutoRelease(10);
        clue.AutoReleaseAfterMinutes.Should().Be(10);
    }

    [Fact]
    public void SetAutoRelease_Null_ClearsProperty()
    {
        var clue = Clue.Create(Guid.NewGuid(), Guid.NewGuid(), "Find the fountain", 1, 5).Value;
        clue.SetAutoRelease(null);
        clue.AutoReleaseAfterMinutes.Should().BeNull();
    }
}
