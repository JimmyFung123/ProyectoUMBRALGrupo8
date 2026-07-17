namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using StageService.Domain.MissionLookup;
using StageService.Domain.Stages;
using Xunit;

public class StageMissionActivePolicyTests
{
    [Fact]
    public void BlocksStageMutation_WhenMissionIsNull_ReturnsFalse()
        // Lookup local aún no existe (evento no procesado) → se asume Inactiva.
        => StageMissionActivePolicy.BlocksStageMutation(null).Should().BeFalse();

    [Fact]
    public void BlocksStageMutation_WhenMissionIsActive_ReturnsTrue()
    {
        var mission = MissionLookup.Create(Guid.NewGuid(), "Bosque 2.0", "Active");

        StageMissionActivePolicy.BlocksStageMutation(mission).Should().BeTrue();
    }

    [Fact]
    public void BlocksStageMutation_WhenMissionIsInactive_ReturnsFalse()
    {
        var mission = MissionLookup.Create(Guid.NewGuid(), "Bosque 2.0", "Inactive");

        StageMissionActivePolicy.BlocksStageMutation(mission).Should().BeFalse();
    }
}
