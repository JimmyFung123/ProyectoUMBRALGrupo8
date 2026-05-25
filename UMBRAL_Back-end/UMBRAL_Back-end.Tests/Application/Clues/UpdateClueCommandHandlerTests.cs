namespace UMBRAL_Back_end.Tests.Application.Clues;

using FluentAssertions;
using Moq;
using ClueService.Application.Clues.Commands.UpdateClue;
using ClueService.Domain.Clues;
using Xunit;

public class UpdateClueCommandHandlerTests
{
    private readonly Mock<IClueRepository> _clueRepoMock = new();
    private readonly UpdateClueCommandHandler _handler;

    public UpdateClueCommandHandlerTests()
    {
        _handler = new UpdateClueCommandHandler(_clueRepoMock.Object);
    }

    [Fact]
    public async Task Handle_WhenClueDoesNotExist_ReturnsNotFound()
    {
        var clueId = Guid.NewGuid();
        _clueRepoMock.Setup(r => r.GetByIdAsync(clueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Clue?)null);

        var result = await _handler.Handle(
            new UpdateClueCommand(clueId, 1, "x", null, null, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.NotFound);
    }

    [Fact]
    public async Task Handle_TriviaClue_UpdatesContentAndOrder()
    {
        var stageId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var clue = Clue.Create(stageId, missionId, "Trivia", 1, "Vieja", null, null, null).Value;

        _clueRepoMock.Setup(r => r.GetByIdAsync(clue.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clue);

        var result = await _handler.Handle(
            new UpdateClueCommand(clue.Id, 2, "Nueva", null, null, null), default);

        result.IsSuccess.Should().BeTrue();
        clue.Content.Should().Be("Nueva");
        clue.Order.Should().Be(2);
        _clueRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TreasureClue_UpdatesGeoFields()
    {
        var stageId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var clue = Clue.Create(stageId, missionId, "TreasureHunt", 1, null, 10.0, -66.0, 100).Value;

        _clueRepoMock.Setup(r => r.GetByIdAsync(clue.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clue);

        var result = await _handler.Handle(
            new UpdateClueCommand(clue.Id, 1, null, 10.5, -66.5, 30), default);

        result.IsSuccess.Should().BeTrue();
        clue.Latitude.Should().Be(10.5);
        clue.Longitude.Should().Be(-66.5);
        clue.RadiusMeters.Should().Be(30);
    }

    [Fact]
    public async Task Handle_TreasureClue_WithMissingGeo_ReturnsInvalidGeoData()
    {
        var stageId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var clue = Clue.Create(stageId, missionId, "TreasureHunt", 1, null, 10.0, -66.0, 100).Value;

        _clueRepoMock.Setup(r => r.GetByIdAsync(clue.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clue);

        var result = await _handler.Handle(
            new UpdateClueCommand(clue.Id, 1, null, null, null, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.InvalidGeoData);
        _clueRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
