namespace UMBRAL_Back_end.Tests.Application.Clues;

using FluentAssertions;
using Moq;
using ClueService.Application;
using ClueService.Application.Clues.Commands.RemoveClue;
using ClueService.Domain.Clues;
using ClueService.Domain.Clues.Events;
using Xunit;

public class RemoveClueCommandHandlerTests
{
    private readonly Mock<IClueRepository> _clueRepoMock = new();
    private readonly RemoveClueCommandHandler _handler;

    public RemoveClueCommandHandlerTests()
    {
        _handler = new RemoveClueCommandHandler(_clueRepoMock.Object);
    }

    [Fact]
    public async Task Handle_WhenClueNotFound_ReturnsNotFoundError()
    {
        var clueId = Guid.NewGuid();

        _clueRepoMock
            .Setup(r => r.GetByIdAsync(clueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Clue?)null);

        var result = await _handler.Handle(new RemoveClueCommand(clueId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.NotFound);
        _clueRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Clue>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenClueExists_DeletesAndRaisesDomainEvent()
    {
        var stageId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var clueId = Guid.NewGuid();
        var clue = Clue.Create(stageId, missionId, "Pista a eliminar", 1).Value;

        _clueRepoMock
            .Setup(r => r.GetByIdAsync(clueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clue);

        var result = await _handler.Handle(new RemoveClueCommand(clueId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        _clueRepoMock.Verify(r => r.DeleteAsync(clue, It.IsAny<CancellationToken>()), Times.Once);
        _clueRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        clue.DomainEvents.OfType<ClueRemovedDomainEvent>().Should().ContainSingle()
            .Which.StageId.Should().Be(stageId);
    }
}
