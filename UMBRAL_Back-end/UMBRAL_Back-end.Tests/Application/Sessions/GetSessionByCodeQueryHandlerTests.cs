namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application.Sessions.Queries.GetSessionByCode;
using SessionService.Domain.Sessions;
using Xunit;

public class GetSessionByCodeQueryHandlerTests
{
    private readonly Mock<ISessionRepository> _repoMock = new();
    private readonly GetSessionByCodeQueryHandler _handler;

    public GetSessionByCodeQueryHandlerTests()
        => _handler = new GetSessionByCodeQueryHandler(_repoMock.Object);

    [Fact]
    public async Task Handle_WhenCodeNotFound_ReturnsNotFoundError()
    {
        _repoMock.Setup(r => r.GetByAccessCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Session?)null);

        var result = await _handler.Handle(new GetSessionByCodeQuery("XXXXXX"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenCodeFound_ReturnsSessionInfo()
    {
        var session = Session.Create(Guid.NewGuid(), "My Session").Value;
        _repoMock.Setup(r => r.GetByAccessCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(session);

        var result = await _handler.Handle(new GetSessionByCodeQuery(session.AccessCode), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(session.Id);
        result.Value.Name.Should().Be("My Session");
        result.Value.AccessCode.Should().Be(session.AccessCode);
    }
}
