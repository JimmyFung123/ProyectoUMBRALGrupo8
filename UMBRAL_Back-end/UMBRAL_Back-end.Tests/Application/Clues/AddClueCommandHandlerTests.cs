namespace UMBRAL_Back_end.Tests.Application.Clues;

using FluentAssertions;
using MassTransit;
using Moq;
using ClueService.Application.Clues.Commands.AddClue;
using ClueService.Domain.Clues;
using ClueService.Domain.StageLookup;
using UMBRAL.Contracts.Events;
using Xunit;

public class AddClueCommandHandlerTests
{
    private readonly Mock<IClueRepository> _clueRepoMock = new();
    private readonly Mock<IStageLookupRepository> _stageLookupMock = new();
    private readonly Mock<IPublishEndpoint> _busMock = new();
    private readonly AddClueCommandHandler _handler;

    public AddClueCommandHandlerTests()
    {
        _handler = new AddClueCommandHandler(
            _clueRepoMock.Object,
            _stageLookupMock.Object,
            _busMock.Object);
    }

    [Fact]
    public async Task Handle_WhenStageNotFoundInLookup_ReturnsStageNotFoundError()
    {
        var stageId = Guid.NewGuid();

        _stageLookupMock
            .Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StageLookup?)null);

        var result = await _handler.Handle(new AddClueCommand(stageId, "Contenido válido"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.StageNotFound);
        _clueRepoMock.Verify(r => r.AddAsync(It.IsAny<Clue>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenContentIsEmpty_ReturnsInvalidContentError()
    {
        var stageId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var stageLookup = StageLookup.Create(stageId, missionId, "Etapa 1");

        _stageLookupMock
            .Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stageLookup);

        _clueRepoMock
            .Setup(r => r.GetByStageIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Clue>());

        var result = await _handler.Handle(new AddClueCommand(stageId, "   "), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.InvalidContent);
    }

    [Fact]
    public async Task Handle_WhenValid_CreatesClueWithCorrectOrderAndPublishesEvent()
    {
        var stageId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var stageLookup = StageLookup.Create(stageId, missionId, "Etapa 1");

        // Simula 2 pistas previas → la nueva pista debe tener orden 3
        var existingClues = new List<Clue>
        {
            Clue.Create(stageId, missionId, "Pista 1", 1).Value,
            Clue.Create(stageId, missionId, "Pista 2", 2).Value,
        };

        _stageLookupMock
            .Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stageLookup);

        _clueRepoMock
            .Setup(r => r.GetByStageIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingClues);

        var result = await _handler.Handle(new AddClueCommand(stageId, "Busca debajo del árbol"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);
        _clueRepoMock.Verify(r => r.AddAsync(
            It.Is<Clue>(c => c.Order == 3 && c.Content == "Busca debajo del árbol"),
            It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(
            b => b.Publish(It.IsAny<ClueAddedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
