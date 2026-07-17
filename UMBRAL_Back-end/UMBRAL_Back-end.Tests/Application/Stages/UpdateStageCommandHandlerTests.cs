namespace UMBRAL_Back_end.Tests.Application.Stages;

using FluentAssertions;
using Moq;
using StageService.Application.Stages.Commands.UpdateStage;
using StageService.Domain.MissionLookup;
using StageService.Domain.Stages;
using Xunit;

public class UpdateStageCommandHandlerTests
{
    private readonly Mock<IStageRepository> _stageRepoMock = new();
    private readonly Mock<IMissionLookupRepository> _missionLookupMock = new();
    private readonly UpdateStageCommandHandler _handler;

    public UpdateStageCommandHandlerTests()
    {
        _handler = new UpdateStageCommandHandler(
            _stageRepoMock.Object,
            _missionLookupMock.Object);
    }

    [Fact]
    public async Task Handle_WhenStageNotFound_ReturnsNotFoundError()
    {
        var stageId = Guid.NewGuid();

        _stageRepoMock
            .Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stage?)null);

        var result = await _handler.Handle(ValidCommand(stageId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.NotFound);
        _stageRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMissionIsActive_ReturnsMissionIsActiveError()
    {
        var missionId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var stage = Stage.Create(missionId, "Etapa original", StageType.Trivia, 1, 100).Value;
        var activeMission = MissionLookup.Create(missionId, "Active Mission", "Active");

        _stageRepoMock
            .Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stage);

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeMission);

        var result = await _handler.Handle(ValidCommand(stageId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.MissionIsActive);
        _stageRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenStageExistsAndMissionIsInactive_UpdatesAndSaves()
    {
        var missionId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var stage = Stage.Create(missionId, "Título original", StageType.Trivia, 1, 50).Value;
        var inactiveMission = MissionLookup.Create(missionId, "Inactive Mission", "Inactive");

        _stageRepoMock
            .Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stage);

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveMission);

        var command = new UpdateStageCommand(
            stageId, "Título actualizado", Order: 2, BaseScore: 200,
            Question: "Nueva pregunta", Options: null,
            Latitude: null, Longitude: null, QrCode: null,
            AutoReleaseTimeMinutes: null, AutoReleaseMaxAttempts: null);

        var result = await _handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        stage.Title.Should().Be("Título actualizado");
        stage.BaseScore.Should().Be(200);
        _stageRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTreasureHuntQrCodeIsDuplicate_ReturnsDuplicateQrCodeError()
    {
        var missionId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var stage = Stage.Create(missionId, "Tesoro original", StageType.TreasureHunt, 1, 50,
            latitude: 10.0, longitude: -66.0, qrCode: "QR-OLD").Value;
        var inactiveMission = MissionLookup.Create(missionId, "Inactive Mission", "Inactive");

        _stageRepoMock
            .Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stage);

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveMission);

        _stageRepoMock
            .Setup(r => r.ExistsWithQrCodeAsync("QR-TAKEN", stage.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new UpdateStageCommand(
            stageId, "Tesoro actualizado", Order: 1, BaseScore: 50,
            Question: null, Options: null,
            Latitude: 10.0, Longitude: -66.0, QrCode: "QR-TAKEN",
            AutoReleaseTimeMinutes: null, AutoReleaseMaxAttempts: null);

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.DuplicateQrCode);
        _stageRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTreasureHuntQrCodeIsUnchanged_UpdatesSuccessfully()
    {
        var missionId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var stage = Stage.Create(missionId, "Tesoro original", StageType.TreasureHunt, 1, 50,
            latitude: 10.0, longitude: -66.0, qrCode: "QR-SAME").Value;
        var inactiveMission = MissionLookup.Create(missionId, "Inactive Mission", "Inactive");

        _stageRepoMock
            .Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stage);

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveMission);

        _stageRepoMock
            .Setup(r => r.ExistsWithQrCodeAsync("QR-SAME", stage.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new UpdateStageCommand(
            stageId, "Tesoro renombrado", Order: 1, BaseScore: 75,
            Question: null, Options: null,
            Latitude: 10.0, Longitude: -66.0, QrCode: "QR-SAME",
            AutoReleaseTimeMinutes: null, AutoReleaseMaxAttempts: null);

        var result = await _handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        _stageRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTriviaOptionsProvided_RemovesOldAndAddsNew()
    {
        var missionId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var stage = Stage.Create(missionId, "Trivia original", StageType.Trivia, 1, 50,
            question: "Pregunta vieja").Value;
        var inactiveMission = MissionLookup.Create(missionId, "Inactive Mission", "Inactive");

        _stageRepoMock
            .Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stage);

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveMission);

        _stageRepoMock
            .Setup(r => r.RemoveOptionsAsync(stage.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _stageRepoMock
            .Setup(r => r.AddOptionsAsync(It.IsAny<IEnumerable<TriviaOption>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new UpdateStageCommand(
            stageId, "Trivia actualizada", Order: 1, BaseScore: 50,
            Question: "Pregunta nueva",
            Options: [("Opción A", true), ("Opción B", false)],
            Latitude: null, Longitude: null, QrCode: null,
            AutoReleaseTimeMinutes: null, AutoReleaseMaxAttempts: null);

        var result = await _handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        _stageRepoMock.Verify(r => r.RemoveOptionsAsync(stage.Id, It.IsAny<CancellationToken>()), Times.Once);
        _stageRepoMock.Verify(
            r => r.AddOptionsAsync(
                It.Is<IEnumerable<TriviaOption>>(opts => opts.Count() == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _stageRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static UpdateStageCommand ValidCommand(Guid stageId) =>
        new(stageId, "Título de prueba", Order: 1, BaseScore: 100,
            Question: "Pregunta", Options: null,
            Latitude: null, Longitude: null, QrCode: null,
            AutoReleaseTimeMinutes: null, AutoReleaseMaxAttempts: null);
}
