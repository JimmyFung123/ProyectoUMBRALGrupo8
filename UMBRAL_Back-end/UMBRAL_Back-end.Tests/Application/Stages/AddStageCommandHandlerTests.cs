namespace UMBRAL_Back_end.Tests.Application.Stages;

using FluentAssertions;
using MassTransit;
using Moq;
using StageService.Application.Stages.Commands.AddStage;
using StageService.Domain.MissionLookup;
using StageService.Domain.Stages;
using UMBRAL.Contracts.Events;
using Xunit;

public class AddStageCommandHandlerTests
{
    private readonly Mock<IStageRepository> _stageRepoMock = new();
    private readonly Mock<IMissionLookupRepository> _missionLookupMock = new();
    private readonly Mock<IPublishEndpoint> _busMock = new();
    private readonly AddStageCommandHandler _handler;

    public AddStageCommandHandlerTests()
    {
        _handler = new AddStageCommandHandler(
            _stageRepoMock.Object,
            _missionLookupMock.Object,
            _busMock.Object);
    }

    [Fact]
    public async Task Handle_WhenMissionIsActive_ReturnsMissionIsActiveError()
    {
        var missionId = Guid.NewGuid();
        var activeMission = MissionLookup.Create(missionId, "Active Mission", "Active");

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeMission);

        var command = ValidCommand(missionId);

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.MissionIsActive);
        _stageRepoMock.Verify(r => r.AddAsync(It.IsAny<Stage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMissionLookupDoesNotExist_CreatesStageSuccessfully()
    {
        // Pragmatic fallback: null lookup → mission treated as Inactive → proceed
        var missionId = Guid.NewGuid();

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MissionLookup?)null);

        var command = ValidCommand(missionId);

        var result = await _handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);
        _stageRepoMock.Verify(r => r.AddAsync(It.IsAny<Stage>(), It.IsAny<CancellationToken>()), Times.Once);
        _stageRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenMissionIsInactive_CreatesStageAndPublishesEvent()
    {
        var missionId = Guid.NewGuid();
        var inactiveMission = MissionLookup.Create(missionId, "Inactive Mission", "Inactive");

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveMission);

        var command = ValidCommand(missionId);

        var result = await _handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        _busMock.Verify(
            b => b.Publish(It.IsAny<StageAddedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenStageTypeIsInvalid_ReturnsInvalidTypeError()
    {
        var missionId = Guid.NewGuid();

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MissionLookup?)null);

        var command = new AddStageCommand(missionId, "Valid Title", "TipoInvalido",
            Order: 1, BaseScore: 10, Question: null, Options: null,
            Latitude: null, Longitude: null, QrCode: null);

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Stage.InvalidType");
    }

    [Fact]
    public async Task Handle_WhenTitleIsEmpty_ReturnsInvalidNameError()
    {
        var missionId = Guid.NewGuid();

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MissionLookup?)null);

        var command = new AddStageCommand(missionId, "   ", "Trivia",
            Order: 1, BaseScore: 10, Question: null, Options: null,
            Latitude: null, Longitude: null, QrCode: null);

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.InvalidName);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static AddStageCommand ValidCommand(Guid missionId) =>
        new(missionId, "Etapa de prueba", "Trivia",
            Order: 1, BaseScore: 100, Question: "¿Cuál es la respuesta?", Options: null,
            Latitude: null, Longitude: null, QrCode: null);
}
