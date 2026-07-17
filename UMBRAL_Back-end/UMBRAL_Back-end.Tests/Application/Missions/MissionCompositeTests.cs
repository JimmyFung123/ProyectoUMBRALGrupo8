namespace UMBRAL_Back_end.Tests.Application.Missions;

using FluentAssertions;
using SessionService.Application.Missions.Composite;
using Xunit;

public class MissionCompositeTests
{
    private static MissionComponent BuildSampleMission()
    {
        // Mission
        //  ├─ Stage 1 (Trivia, 100)  ├─ Clue, Clue
        //  ├─ Stage 2 (Trivia, 50)   └─ (no clues)
        //  └─ Stage 3 (TreasureHunt, 200) └─ Clue
        var mission = new MissionComponent(Guid.NewGuid(), "Misión X", "Hard", "Active");

        var s1 = new StageComponent(Guid.NewGuid(), "Etapa 1", "Trivia", 1, 100);
        s1.Add(new ClueComponent(Guid.NewGuid(), 1, "Pista 1"));
        s1.Add(new ClueComponent(Guid.NewGuid(), 2, "Pista 2"));

        var s2 = new StageComponent(Guid.NewGuid(), "Etapa 2", "Trivia", 2, 50);

        var s3 = new StageComponent(Guid.NewGuid(), "Etapa 3", "TreasureHunt", 3, 200);
        s3.Add(new ClueComponent(Guid.NewGuid(), 1, "Pista A"));

        mission.Add(s1);
        mission.Add(s2);
        mission.Add(s3);
        return mission;
    }

    [Fact]
    public void TotalScore_OfMission_SumsStageBaseScores_AndIgnoresClues()
    {
        var mission = BuildSampleMission();

        mission.TotalScore().Should().Be(350); // 100 + 50 + 200
    }

    [Fact]
    public void TotalScore_OfStage_EqualsBaseScore_RegardlessOfClues()
    {
        var stage = new StageComponent(Guid.NewGuid(), "Etapa", "Trivia", 1, 120);
        stage.Add(new ClueComponent(Guid.NewGuid(), 1, "c1"));
        stage.Add(new ClueComponent(Guid.NewGuid(), 2, "c2"));

        stage.TotalScore().Should().Be(120);
    }

    [Fact]
    public void Clue_IsLeaf_TotalScoreZero_AndHasNoChildren()
    {
        var clue = new ClueComponent(Guid.NewGuid(), 1, "Pista");

        clue.TotalScore().Should().Be(0);
        clue.Children.Should().BeEmpty();
        clue.ComponentType.Should().Be("Clue");
    }

    [Fact]
    public void Clue_Add_Throws_BecauseItIsALeaf()
    {
        var clue = new ClueComponent(Guid.NewGuid(), 1, "Pista");

        var act = () => clue.Add(new ClueComponent(Guid.NewGuid(), 2, "otra"));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Remove_DetachesChildFromComposite()
    {
        var stage = new StageComponent(Guid.NewGuid(), "Etapa", "Trivia", 1, 100);
        var clue = new ClueComponent(Guid.NewGuid(), 1, "Pista");
        stage.Add(clue);

        stage.Children.Should().HaveCount(1);
        stage.Remove(clue);
        stage.Children.Should().BeEmpty();
    }

    [Fact]
    public void ClueComponent_Name_FallsBackToOrder_WhenContentIsNullOrEmpty()
    {
        new ClueComponent(Guid.NewGuid(), 7, null).Name.Should().Be("Pista 7");
        new ClueComponent(Guid.NewGuid(), 3, "").Name.Should().Be("Pista 3");
        new ClueComponent(Guid.NewGuid(), 1, "Acércate al árbol").Name.Should().Be("Acércate al árbol");
    }

    [Fact]
    public void Accept_SummaryVisitor_WalksWholeTree_AndAggregatesCounts()
    {
        var mission = BuildSampleMission();
        var visitor = new MissionSummaryVisitor();

        mission.Accept(visitor);
        var summary = visitor.Build();

        summary.StageCount.Should().Be(3);
        summary.ClueCount.Should().Be(3);
        summary.TriviaStages.Should().Be(2);
        summary.TreasureHuntStages.Should().Be(1);
        summary.TotalScore.Should().Be(350);
        summary.StagesWithoutClues.Should().Be(1); // Stage 2
    }

    [Fact]
    public void Accept_OnEmptyMission_ProducesZeroedSummary()
    {
        var mission = new MissionComponent(Guid.NewGuid(), "Vacía", "Easy", "Inactive");
        var visitor = new MissionSummaryVisitor();

        mission.Accept(visitor);
        var summary = visitor.Build();

        summary.StageCount.Should().Be(0);
        summary.ClueCount.Should().Be(0);
        summary.TotalScore.Should().Be(0);
        summary.StagesWithoutClues.Should().Be(0);
    }
}
