namespace UMBRAL_Back_end.Tests.Application.Clues;

using FluentAssertions;
using Moq;
using ClueService.Application;
using ClueService.Application.Clues.Commands.AddClue;
using ClueService.Domain.Clues;
using ClueService.Domain.StageLookup;
using UMBRAL.Contracts.Events;
using Xunit;

public class AddClueCommandHandlerTests
{
    private readonly Mock<IClueRepository> _clueRepoMock = new();
    private readonly Mock<IStageLookupRepository> _stageLookupMock = new();
    private readonly Mock<IIntegrationEventBus> _busMock = new();
    private readonly AddClueCommandHandler _handler;

    public AddClueCommandHandlerTests()
    {
        _handler = new AddClueCommandHandler(
            _clueRepoMock.Object,
            _stageLookupMock.Object,
            _busMock.Object);
    }

    private AddClueCommand TriviaCmd(Guid stageId, int order = 0, string? content = "Contenido válido")
        => new(stageId, order, content, null, null, null);

    private AddClueCommand TreasureCmd(Guid stageId, int order = 0, double? lat = 10.48, double? lng = -66.85, int? radius = 50)
        => new(stageId, order, null, lat, lng, radius);

    [Fact]
    public async Task Handle_WhenStageNotFoundInLookup_ReturnsStageNotFoundError()
    {
        var stageId = Guid.NewGuid();

        _stageLookupMock
            .Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StageLookup?)null);

        var result = await _handler.Handle(TriviaCmd(stageId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.StageNotFound);
        _clueRepoMock.Verify(r => r.AddAsync(It.IsAny<Clue>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TriviaStage_WhenContentIsEmpty_ReturnsInvalidContentError()
    {
        var stageId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var stageLookup = StageLookup.Create(stageId, missionId, "Trivia");

        _stageLookupMock.Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stageLookup);
        _clueRepoMock.Setup(r => r.GetByStageIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Clue>());

        var result = await _handler.Handle(TriviaCmd(stageId, content: "   "), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.InvalidContent);
    }

    [Fact]
    public async Task Handle_TriviaValid_CreatesClueWithComputedOrderAndPublishesEvent()
    {
        var stageId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var stageLookup = StageLookup.Create(stageId, missionId, "Trivia");

        var existingClues = new List<Clue>
        {
            Clue.Create(stageId, missionId, "Pista 1", 1).Value,
            Clue.Create(stageId, missionId, "Pista 2", 2).Value,
        };

        _stageLookupMock.Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stageLookup);
        _clueRepoMock.Setup(r => r.GetByStageIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingClues);

        var result = await _handler.Handle(TriviaCmd(stageId, content: "Busca debajo del árbol"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);
        _clueRepoMock.Verify(r => r.AddAsync(
            It.Is<Clue>(c => c.Order == 3 && c.Content == "Busca debajo del árbol" && c.StageType == "Trivia"),
            It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(
            b => b.PublishAsync(It.IsAny<ClueAddedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_TreasureHuntValid_CreatesClueWithGeoData()
    {
        var stageId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var stageLookup = StageLookup.Create(stageId, missionId, "TreasureHunt");

        _stageLookupMock.Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stageLookup);
        _clueRepoMock.Setup(r => r.GetByStageIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Clue>());

        var result = await _handler.Handle(TreasureCmd(stageId, lat: 10.49, lng: -66.85, radius: 75), default);

        result.IsSuccess.Should().BeTrue();
        _clueRepoMock.Verify(r => r.AddAsync(
            It.Is<Clue>(c =>
                c.StageType == "TreasureHunt" &&
                c.Content == null &&
                c.Latitude == 10.49 &&
                c.Longitude == -66.85 &&
                c.RadiusMeters == 75),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TreasureHunt_WithoutGeoData_ReturnsInvalidGeoData()
    {
        var stageId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var stageLookup = StageLookup.Create(stageId, missionId, "TreasureHunt");

        _stageLookupMock.Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stageLookup);
        _clueRepoMock.Setup(r => r.GetByStageIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Clue>());

        var result = await _handler.Handle(TreasureCmd(stageId, lat: null, lng: null, radius: null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.InvalidGeoData);
    }

    [Fact]
    public async Task Handle_WhenOrderSpecified_UsesProvidedOrderInsteadOfComputed()
    {
        var stageId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var stageLookup = StageLookup.Create(stageId, missionId, "Trivia");

        _stageLookupMock.Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stageLookup);
        _clueRepoMock.Setup(r => r.GetByStageIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Clue>());

        var result = await _handler.Handle(TriviaCmd(stageId, order: 7, content: "Manual order"), default);

        result.IsSuccess.Should().BeTrue();
        _clueRepoMock.Verify(r => r.AddAsync(
            It.Is<Clue>(c => c.Order == 7),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
