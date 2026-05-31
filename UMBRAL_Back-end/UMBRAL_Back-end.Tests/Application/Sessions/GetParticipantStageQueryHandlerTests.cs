namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application.Sessions.Facade;
using SessionService.Application.Sessions.Queries.GetParticipantStage;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using Xunit;

/// <summary>
/// Tras aplicar el patrón Facade, el handler ya no orquesta: solo delega en
/// <see cref="IParticipantStageFacade"/>. Estos tests verifican esa delegación.
/// </summary>
public class GetParticipantStageQueryHandlerTests
{
    private readonly Mock<IParticipantStageFacade> _facadeMock = new();
    private readonly GetParticipantStageQueryHandler _handler;

    public GetParticipantStageQueryHandlerTests()
    {
        _handler = new GetParticipantStageQueryHandler(_facadeMock.Object);
    }

    [Fact]
    public async Task Handle_DelegatesToFacadeWithQueryArguments()
    {
        var sessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var expected = Result.Success(new ParticipantStageDto(
            StageId: Guid.NewGuid(),
            Title: "Etapa 1",
            Type: "Trivia",
            Order: 1,
            Question: "¿?",
            Options: new List<ParticipantOptionDto>(),
            SessionStatus: "InProgress",
            CurrentStageOrder: 1,
            IsLastStage: true));

        _facadeMock
            .Setup(f => f.GetCurrentStageAsync(sessionId, teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _handler.Handle(new GetParticipantStageQuery(sessionId, teamId), default);

        result.Should().Be(expected);
        _facadeMock.Verify(
            f => f.GetCurrentStageAsync(sessionId, teamId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PropagatesFacadeFailure()
    {
        var failure = Result.Failure<ParticipantStageDto>(SessionErrors.TeamNotFound);

        _facadeMock
            .Setup(f => f.GetCurrentStageAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        var result = await _handler.Handle(
            new GetParticipantStageQuery(Guid.NewGuid(), Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.TeamNotFound);
    }
}
