namespace UMBRAL_Back_end.Tests.Application.Teams;

using FluentAssertions;
using Moq;
using TeamService.Application.Teams.Queries.GetSessionRanking;
using TeamService.Domain.Rankings;
using Xunit;

/// <summary>
/// HU-24: optimized CQRS read model.
///
/// After HU-24 the query handler is a pure mapper: it pulls the pre-sorted,
/// pre-ranked rows from <see cref="IRankingProjectionRepository"/> and copies
/// them onto the DTO. No sorting, no rank arithmetic, no business logic.
///
/// The sort/rank correctness is now covered by <c>RankingProjectorTests</c>.
/// </summary>
public class GetSessionRankingQueryHandlerTests
{
    private readonly Mock<IRankingProjectionRepository> _rankingRepoMock = new();
    private readonly GetSessionRankingQueryHandler _handler;

    public GetSessionRankingQueryHandlerTests()
    {
        _handler = new GetSessionRankingQueryHandler(_rankingRepoMock.Object);
    }

    [Fact]
    public async Task Handle_WhenNoRows_ReturnsEmptySnapshot()
    {
        var sessionId = Guid.NewGuid();
        _rankingRepoMock
            .Setup(r => r.GetBySessionIdOrderedAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RankingProjection>());

        var result = await _handler.Handle(new GetSessionRankingQuery(sessionId), default);

        result.SessionId.Should().Be(sessionId);
        result.Teams.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsRowsInRepositoryOrder_WithoutResorting()
    {
        // The repository is the source of truth for order — the handler must NOT
        // re-sort. We deliberately feed rows in repo order and assert the DTOs
        // come out in the exact same sequence.
        var sessionId = Guid.NewGuid();
        var rows = new List<RankingProjection>
        {
            MakeRow(sessionId, "Líder",   score: 300, rank: 1, position: 1),
            MakeRow(sessionId, "Segundo", score: 200, rank: 2, position: 2),
            MakeRow(sessionId, "Tercero", score: 100, rank: 3, position: 3),
        };

        _rankingRepoMock
            .Setup(r => r.GetBySessionIdOrderedAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var result = await _handler.Handle(new GetSessionRankingQuery(sessionId), default);

        result.Teams.Select(t => t.Name).Should().Equal("Líder", "Segundo", "Tercero");
        result.Teams.Select(t => t.Rank).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Handle_MapsEveryFieldOneToOne()
    {
        var sessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var resolutionTime = new DateTime(2026, 5, 26, 10, 30, 0, DateTimeKind.Utc);

        var row = RankingProjection.Create(
            sessionId: sessionId,
            teamId: teamId,
            teamName: "Equipo Delta",
            score: 250,
            rank: 1,
            position: 1,
            currentStageOrder: 4,
            isConnected: true,
            lastStageCompletedAt: resolutionTime,
            updatedAt: DateTime.UtcNow);

        _rankingRepoMock
            .Setup(r => r.GetBySessionIdOrderedAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RankingProjection> { row });

        var result = await _handler.Handle(new GetSessionRankingQuery(sessionId), default);

        var dto = result.Teams.Single();
        dto.TeamId.Should().Be(teamId);
        dto.Name.Should().Be("Equipo Delta");
        dto.Score.Should().Be(250);
        dto.Rank.Should().Be(1);
        dto.CurrentStageOrder.Should().Be(4);
        dto.IsConnected.Should().BeTrue();
        dto.LastStageCompletedAt.Should().Be(resolutionTime);
    }

    [Fact]
    public async Task Handle_PopulatesGeneratedAt()
    {
        var sessionId = Guid.NewGuid();
        _rankingRepoMock
            .Setup(r => r.GetBySessionIdOrderedAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RankingProjection>());

        var before = DateTime.UtcNow;
        var result = await _handler.Handle(new GetSessionRankingQuery(sessionId), default);
        var after = DateTime.UtcNow;

        result.GeneratedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task Handle_DoesNotTouchTransactionalReposOrComputeAnything()
    {
        // Sanity check that the handler depends *only* on the projection repo.
        // No ITeamRepository injection, no team aggregate, no joins.
        var ctor = typeof(GetSessionRankingQueryHandler)
            .GetConstructors().Single();

        ctor.GetParameters().Should().HaveCount(1);
        ctor.GetParameters()[0].ParameterType
            .Should().Be(typeof(IRankingProjectionRepository));
    }

    private static RankingProjection MakeRow(
        Guid sessionId, string name, int score, int rank, int position) =>
        RankingProjection.Create(
            sessionId: sessionId,
            teamId: Guid.NewGuid(),
            teamName: name,
            score: score,
            rank: rank,
            position: position,
            currentStageOrder: 1,
            isConnected: true,
            lastStageCompletedAt: null,
            updatedAt: DateTime.UtcNow);
}
