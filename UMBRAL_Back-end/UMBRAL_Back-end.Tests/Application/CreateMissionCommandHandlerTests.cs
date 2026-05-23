namespace UMBRAL_Back_end.Tests.Application;

using FluentAssertions;
using MassTransit;
using MediatR;
using Moq;
using UMBRAL_Back_end.Application.Missions.Commands.CreateMission;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Domain.Missions.Events;
using Xunit;

public class CreateMissionCommandHandlerTests
{
    private readonly Mock<IMissionRepository> _repositoryMock = new();
    private readonly Mock<IPublisher> _publisherMock = new();
    private readonly Mock<IPublishEndpoint> _busMock = new();
    private readonly CreateMissionCommandHandler _handler;

    public CreateMissionCommandHandlerTests()
    {
        _handler = new CreateMissionCommandHandler(_repositoryMock.Object, _publisherMock.Object, _busMock.Object);
    }

    [Fact]
    public async Task Handle_WhenNameIsUnique_CreatesMissionAndPublishesEvent()
    {
        _repositoryMock
            .Setup(r => r.ExistsWithNameAsync(It.IsAny<string>(), null, default))
            .ReturnsAsync(false);

        var command = new CreateMissionCommand("Alpha Protocol", "desc", DifficultyLevel.Medium, 60);

        var result = await _handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Mission>(), default), Times.Once);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Once);

        _publisherMock.Verify(
            p => p.Publish(It.IsAny<MissionCreatedEvent>(), default),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNameAlreadyExists_ReturnsDuplicateNameError()
    {
        _repositoryMock
            .Setup(r => r.ExistsWithNameAsync("Duplicate", null, default))
            .ReturnsAsync(true);

        var command = new CreateMissionCommand("Duplicate", "desc", DifficultyLevel.Easy, 30);

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.DuplicateName);

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Mission>(), default), Times.Never);
        _publisherMock.Verify(p => p.Publish(It.IsAny<INotification>(), default), Times.Never);
    }
}
