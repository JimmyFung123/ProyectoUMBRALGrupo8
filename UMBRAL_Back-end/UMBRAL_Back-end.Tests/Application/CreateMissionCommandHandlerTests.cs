namespace UMBRAL_Back_end.Tests.Application;

using FluentAssertions;
using Moq;
using UMBRAL.Contracts.Events;
using UMBRAL_Back_end.Application;
using UMBRAL_Back_end.Application.Missions.Commands.CreateMission;
using UMBRAL_Back_end.Domain.Missions;
using Xunit;

public class CreateMissionCommandHandlerTests
{
    private readonly Mock<IMissionRepository> _repositoryMock = new();
    private readonly Mock<IIntegrationEventBus> _busMock = new();
    private readonly CreateMissionCommandHandler _handler;

    public CreateMissionCommandHandlerTests()
    {
        _handler = new CreateMissionCommandHandler(_repositoryMock.Object, _busMock.Object);
    }

    [Fact]
    public async Task Handle_WhenNameIsUnique_CreatesMissionAndPublishesEvent()
    {
        _repositoryMock
            .Setup(r => r.ExistsWithNameAsync(It.IsAny<string>(), null, default))
            .ReturnsAsync(false);

        var command = new CreateMissionCommand("Alpha Protocol", "desc", "Medium", 60);

        var result = await _handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Mission>(), default), Times.Once);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Once);

        _busMock.Verify(
            b => b.PublishAsync(It.IsAny<MissionCreatedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNameAlreadyExists_ReturnsDuplicateNameError()
    {
        _repositoryMock
            .Setup(r => r.ExistsWithNameAsync("Duplicate", null, default))
            .ReturnsAsync(true);

        var command = new CreateMissionCommand("Duplicate", "desc", "Easy", 30);

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.DuplicateName);

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Mission>(), default), Times.Never);
        _busMock.Verify(
            b => b.PublishAsync(It.IsAny<MissionCreatedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
