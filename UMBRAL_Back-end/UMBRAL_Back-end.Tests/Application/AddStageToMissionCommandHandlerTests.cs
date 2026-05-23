namespace UMBRAL_Back_end.Tests.Application;

using FluentAssertions;
using MediatR;
using Moq;
using UMBRAL_Back_end.Application.Missions.Commands.AddStageToMission;
using UMBRAL_Back_end.Domain.Missions;
using Xunit;

public class AddStageToMissionCommandHandlerTests
{
    private readonly Mock<IMissionRepository> _repositoryMock = new();
    private readonly Mock<IPublisher> _publisherMock = new();
    private readonly AddStageToMissionCommandHandler _handler;

    public AddStageToMissionCommandHandlerTests()
    {
        _handler = new AddStageToMissionCommandHandler(_repositoryMock.Object, _publisherMock.Object);
    }

    [Fact]
    public async Task Handle_AddTriviaStage_WhenMissionExistsAndIsInactive_Succeeds()
    {
        var mission = Mission.Create("Test Mission", "desc", DifficultyLevel.Medium, 60).Value;

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);

        var command = new AddStageToMissionCommand(
            MissionId: mission.Id,
            Title: "Trivia Stage",
            Order: 1,
            Type: StageType.Trivia,
            BaseScore: 100,
            Question: "What is DDD?",
            Options: [new("Domain Driven Design", true), new("Data Driven Development", false)],
            Latitude: null,
            Longitude: null,
            QrCode: null);

        var result = await _handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);

        // Handler registers the new stage explicitly — UpdateAsync is NOT called
        _repositoryMock.Verify(r => r.AddStageAsync(It.IsAny<MissionStage>(), default), Times.Once);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Mission>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMissionNotFound_ReturnsNotFoundError()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Mission?)null);

        var command = new AddStageToMissionCommand(
            Guid.NewGuid(), "Stage", 1, StageType.Trivia, 100, "Q?", null, null, null, null);

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.NotFound);
        _repositoryMock.Verify(r => r.AddStageAsync(It.IsAny<MissionStage>(), default), Times.Never);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMissionIsActive_ReturnsMissionLockedError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Initial Stage", 1, StageType.Trivia, 50, false, question: "Q?");
        mission.Activate();

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);

        var command = new AddStageToMissionCommand(
            mission.Id, "New Stage", 2, StageType.Trivia, 100, "Q2?", null, null, null, null);

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.MissionLockedForEditing);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_AddTreasureHuntStage_WithDuplicateQrCode_ReturnsDuplicateQrCodeError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasDuplicateQrCodeAsync("QR-TAKEN", null, default)).ReturnsAsync(true);

        var command = new AddStageToMissionCommand(
            mission.Id, "Treasure Stage", 1, StageType.TreasureHunt, 200,
            null, null, 10.5, -66.9, "QR-TAKEN");

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.DuplicateQrCode);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_AddTreasureHuntStage_WithValidData_Succeeds()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);
        _repositoryMock.Setup(r => r.HasDuplicateQrCodeAsync("QR-NEW", null, default)).ReturnsAsync(false);

        var command = new AddStageToMissionCommand(
            mission.Id, "Treasure Stage", 1, StageType.TreasureHunt, 200,
            null, null, 10.48801, -66.87919, "QR-NEW");

        var result = await _handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        _repositoryMock.Verify(r => r.AddStageAsync(It.IsAny<MissionStage>(), default), Times.Once);
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Mission>(), default), Times.Never);
    }
}
