namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Queries.GetParticipantStage;
using Xunit;

/// <summary>
/// Pruebas del mapper de la vista de participante (extraido de la fachada por SRP).
/// Cubren el saneo: ocultar IsCorrect y exponer coordenadas solo en TreasureHunt.
/// </summary>
public class ParticipantStageMapperTests
{
    private static StageWithOptionsInfo TriviaStage(Guid id) =>
        new(id, "Etapa 1", "Trivia", 1, 100, "¿Capital de Panamá?",
            new List<TriviaOptionInfo>
            {
                new(Guid.NewGuid(), "Ciudad de Panamá", IsCorrect: true),
                new(Guid.NewGuid(), "Colón", IsCorrect: false),
            },
            Latitude: 8.98, Longitude: -79.51); // coords presentes pero NO es TreasureHunt

    [Fact]
    public void FromStage_HidesIsCorrect_AndOmitsCoordinates_ForTrivia()
    {
        var stageId = Guid.NewGuid();
        var stage = TriviaStage(stageId);

        var dto = ParticipantStageMapper.FromStage(stage, "InProgress", currentStageOrder: 1, isLastStage: false);

        dto.StageId.Should().Be(stageId);
        dto.Title.Should().Be("Etapa 1");
        dto.Question.Should().Be("¿Capital de Panamá?");
        dto.SessionStatus.Should().Be("InProgress");
        dto.CurrentStageOrder.Should().Be(1);
        dto.IsLastStage.Should().BeFalse();

        // El DTO de participante expone Id + Text, nunca cuál opción es la correcta.
        dto.Options.Should().HaveCount(2);
        dto.Options.Select(o => o.Id).Should().BeEquivalentTo(stage.Options.Select(o => o.Id));
        dto.Options.Should().AllBeOfType<ParticipantOptionDto>();

        // Las coordenadas solo se exponen en TreasureHunt.
        dto.Latitude.Should().BeNull();
        dto.Longitude.Should().BeNull();
    }

    [Fact]
    public void FromStage_ExposesCoordinates_ForTreasureHunt_AndKeepsQrServerSide()
    {
        var stageId = Guid.NewGuid();
        var stage = new StageWithOptionsInfo(
            stageId, "El Tesoro", "TreasureHunt", 2, 250, null,
            new List<TriviaOptionInfo>(),
            Latitude: 10.0, Longitude: -66.0, QrCode: "SECRET-QR");

        var dto = ParticipantStageMapper.FromStage(stage, "InProgress", currentStageOrder: 2, isLastStage: true);

        dto.Type.Should().Be("TreasureHunt");
        dto.IsLastStage.Should().BeTrue();
        dto.Latitude.Should().Be(10.0);
        dto.Longitude.Should().Be(-66.0);
        dto.Options.Should().BeEmpty();
    }

    [Fact]
    public void Waiting_ProducesWaitingSentinel()
    {
        var dto = ParticipantStageMapper.Waiting("Pending");

        dto.StageId.Should().Be(Guid.Empty);
        dto.Title.Should().Be("Waiting");
        dto.Type.Should().Be("Waiting");
        dto.CurrentStageOrder.Should().Be(0);
        dto.IsLastStage.Should().BeFalse();
        dto.SessionStatus.Should().Be("Pending");
        dto.Options.Should().BeEmpty();
    }

    [Fact]
    public void Completed_ProducesCompletedSentinel()
    {
        var dto = ParticipantStageMapper.Completed("InProgress", currentStageOrder: 5);

        dto.StageId.Should().Be(Guid.Empty);
        dto.Title.Should().Be("Completed");
        dto.Type.Should().Be("Completed");
        dto.CurrentStageOrder.Should().Be(5);
        dto.IsLastStage.Should().BeTrue();
        dto.SessionStatus.Should().Be("InProgress");
    }
}
