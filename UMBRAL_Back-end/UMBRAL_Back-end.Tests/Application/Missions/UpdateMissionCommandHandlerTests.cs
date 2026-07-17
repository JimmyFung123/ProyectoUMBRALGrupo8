namespace UMBRAL_Back_end.Tests.Application.Missions;

using FluentAssertions;
using Moq;
using UMBRAL.Contracts.Events;
using UMBRAL_Back_end.Application;
using UMBRAL_Back_end.Application.Missions;
using UMBRAL_Back_end.Application.Missions.Commands.UpdateMission;
using UMBRAL_Back_end.Domain.Missions;
using Xunit;

public class UpdateMissionCommandHandlerTests
{
    private readonly Mock<IMissionRepository> _repoMock = new();
    private readonly Mock<IIntegrationEventBus> _busMock = new();
    private readonly Mock<ISessionServiceClient> _sessionClientMock = new();
    private readonly UpdateMissionCommandHandler _handler;

    public UpdateMissionCommandHandlerTests()
        => _handler = new UpdateMissionCommandHandler(_repoMock.Object, _busMock.Object, _sessionClientMock.Object);

    private static UpdateMissionCommand ValidCommand(Guid id) =>
        new(id, "Nuevo nombre", "Nueva descripción", "Medium", 60);

    [Fact]
    public async Task Handle_WhenMissionNotFound_ReturnsNotFoundError()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Mission?)null);

        var result = await _handler.Handle(ValidCommand(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.NotFound);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNameConflicts_ReturnsDuplicateNameError()
    {
        var mission = Mission.Create("Original", "d", DifficultyLevel.Easy, 30).Value;
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(mission);
        _repoMock.Setup(r => r.ExistsWithNameAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

        var result = await _handler.Handle(ValidCommand(mission.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.DuplicateName);
        _busMock.Verify(b => b.PublishAsync(It.IsAny<MissionUpdatedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDifficultyInvalid_ReturnsInvalidDifficultyError()
    {
        var mission = Mission.Create("Original", "d", DifficultyLevel.Easy, 30).Value;
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(mission);
        _repoMock.Setup(r => r.ExistsWithNameAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);

        var result = await _handler.Handle(
            new UpdateMissionCommand(mission.Id, "Nombre", "desc", "Imposible", 60), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.InvalidDifficulty);
    }

    [Fact]
    public async Task Handle_ValidUpdate_SavesAndPublishesEvent()
    {
        var mission = Mission.Create("Original", "d", DifficultyLevel.Easy, 30).Value;
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(mission);
        _repoMock.Setup(r => r.ExistsWithNameAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);
        _sessionClientMock.Setup(c => c.HasActiveSessionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(false);

        var result = await _handler.Handle(ValidCommand(mission.Id), default);

        result.IsSuccess.Should().BeTrue();
        _repoMock.Verify(r => r.UpdateAsync(mission, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(b => b.PublishAsync(It.IsAny<MissionUpdatedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
