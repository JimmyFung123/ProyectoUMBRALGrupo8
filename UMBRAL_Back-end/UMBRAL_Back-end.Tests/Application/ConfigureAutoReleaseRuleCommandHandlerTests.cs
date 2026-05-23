namespace UMBRAL_Back_end.Tests.Application;

using FluentAssertions;
using MediatR;
using Moq;
using UMBRAL_Back_end.Application.Missions.Commands.ConfigureAutoReleaseRule;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Domain.Missions.Events;
using Xunit;

public class ConfigureAutoReleaseRuleCommandHandlerTests
{
    private readonly Mock<IMissionRepository> _repositoryMock = new();
    private readonly Mock<IPublisher> _publisherMock = new();
    private readonly ConfigureAutoReleaseRuleCommandHandler _handler;

    public ConfigureAutoReleaseRuleCommandHandlerTests()
    {
        _handler = new ConfigureAutoReleaseRuleCommandHandler(_repositoryMock.Object, _publisherMock.Object);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static (Mission mission, Guid stageId) MissionWithStage()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Stage", 1, StageType.Trivia, 100, false, question: "Q?");
        var stageId = mission.Stages.First().Id;
        return (mission, stageId);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MissionNotFound_ReturnsMissionNotFoundError()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Mission?)null);

        var command = new ConfigureAutoReleaseRuleCommand(Guid.NewGuid(), Guid.NewGuid(), 5, null);
        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.NotFound);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
        _publisherMock.Verify(p => p.Publish(It.IsAny<INotification>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidTimeMinutes_SavesAndPublishesEvent()
    {
        var (mission, stageId) = MissionWithStage();

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);

        var command = new ConfigureAutoReleaseRuleCommand(mission.Id, stageId, TimeMinutes: 5, MaxAttempts: null);
        var result = await _handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
        _publisherMock.Verify(
            p => p.Publish(
                It.Is<ClueReleaseRuleConfiguredEvent>(e =>
                    e.MissionId == mission.Id &&
                    e.StageId == stageId &&
                    e.TimeMinutes == 5 &&
                    e.MaxAttempts == null),
                default),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidMaxAttempts_SavesAndPublishesEvent()
    {
        var (mission, stageId) = MissionWithStage();

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);

        var command = new ConfigureAutoReleaseRuleCommand(mission.Id, stageId, TimeMinutes: null, MaxAttempts: 3);
        var result = await _handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
        _publisherMock.Verify(
            p => p.Publish(
                It.Is<ClueReleaseRuleConfiguredEvent>(e =>
                    e.MaxAttempts == 3 &&
                    e.TimeMinutes == null),
                default),
            Times.Once);
    }

    [Fact]
    public async Task Handle_MissionLockedForEditing_ReturnsError()
    {
        var (mission, stageId) = MissionWithStage();

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(true);

        var command = new ConfigureAutoReleaseRuleCommand(mission.Id, stageId, TimeMinutes: 5, MaxAttempts: null);
        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.MissionLockedForEditing);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
        _publisherMock.Verify(p => p.Publish(It.IsAny<INotification>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidTimeZero_ReturnsError()
    {
        var (mission, stageId) = MissionWithStage();

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);

        var command = new ConfigureAutoReleaseRuleCommand(mission.Id, stageId, TimeMinutes: 0, MaxAttempts: null);
        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.InvalidAutoReleaseTime);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
        _publisherMock.Verify(p => p.Publish(It.IsAny<INotification>(), default), Times.Never);
    }
}
