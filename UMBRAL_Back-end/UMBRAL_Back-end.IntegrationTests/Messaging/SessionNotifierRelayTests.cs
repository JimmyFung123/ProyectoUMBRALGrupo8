namespace UMBRAL_Back_end.IntegrationTests.Messaging;

using FluentAssertions;
using UMBRAL.Contracts.Events;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using Xunit;
using static UMBRAL_Back_end.IntegrationTests.Infrastructure.Polling;

/// <summary>
/// Level-2: one <c>[Fact]</c> per pure-relay SessionService consumer, all sharing
/// <see cref="SessionNotifierRelayFixture"/>'s single RabbitMQ + Postgres pair. Each test
/// publishes its event over the real broker and polls
/// <see cref="SessionNotifierRelayFixture.NotifierSpy"/> for a matching recorded call,
/// proving MassTransit really delivered the message end-to-end (serialization, routing,
/// queue binding) and the consumer relayed it with the right arguments — Level-1 never
/// exercises the real broker, and none of these consumers write to a DbContext so DB
/// polling isn't an option here.
/// </summary>
[Collection(SessionNotifierRelayCollection.Name)]
public class SessionNotifierRelayTests(SessionNotifierRelayFixture fixture)
{
    [Fact]
    public async Task PublishOperatorMessageBroadcast_OverRealRabbitMq_RelaysToNotifier()
    {
        var sessionId = Guid.NewGuid();
        const string message = "Quedan 5 minutos";
        const string actorName = "Operador Uno";
        var deliveredAt = DateTime.UtcNow;

        await fixture.PublishAsync(new OperatorMessageBroadcastIntegrationEvent(sessionId, message, actorName, deliveredAt));

        var call = await PollAsync(
            () => Task.FromResult(fixture.NotifierSpy.OperatorMessageCalls
                .FirstOrDefault(c => c.SessionId == sessionId)),
            c => c is not null);

        call.Should().NotBeNull();
        call!.Message.Should().Be(message);
        call.ActorName.Should().Be(actorName);
    }

    [Fact]
    public async Task PublishSessionStateChanged_OverRealRabbitMq_RelaysToNotifier()
    {
        var sessionId = Guid.NewGuid();
        const string newStatus = "Paused";

        await fixture.PublishAsync(new SessionStateChangedIntegrationEvent(sessionId, newStatus));

        var call = await PollAsync(
            () => Task.FromResult(fixture.NotifierSpy.StateChangedCalls
                .FirstOrDefault(c => c.SessionId == sessionId)),
            c => c is not null);

        call.Should().NotBeNull();
        call!.NewStatus.Should().Be(newStatus);
    }

    [Fact]
    public async Task PublishStageCompleted_OverRealRabbitMq_RelaysToNotifier()
    {
        var sessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        await fixture.PublishAsync(new StageCompletedIntegrationEvent(
            sessionId, teamId, StageOrder: 2, StageType: "Trivia",
            WasCorrect: true, PointsEarned: 100, NewScore: 300, NextStageOrder: 3, IsLastStage: false));

        var call = await PollAsync(
            () => Task.FromResult(fixture.NotifierSpy.StageCompletedCalls
                .FirstOrDefault(c => c.SessionId == sessionId && c.TeamId == teamId)),
            c => c is not null);

        call.Should().NotBeNull();
        call!.StageOrder.Should().Be(2);
        call.NewScore.Should().Be(300);
        call.IsLastStage.Should().BeFalse();
    }

    [Fact]
    public async Task PublishTeamPenalized_OverRealRabbitMq_RelaysToNotifier()
    {
        var sessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        await fixture.PublishAsync(new TeamPenalizedIntegrationEvent(
            sessionId, teamId, TeamName: "Los Exploradores",
            Points: 50, Reason: "Uso de pista externa", NewScore: 150, ActorName: "Operador Dos"));

        var call = await PollAsync(
            () => Task.FromResult(fixture.NotifierSpy.TeamPenalizedCalls
                .FirstOrDefault(c => c.SessionId == sessionId && c.TeamId == teamId)),
            c => c is not null);

        call.Should().NotBeNull();
        call!.Points.Should().Be(50);
        call.Reason.Should().Be("Uso de pista externa");
    }

    [Fact]
    public async Task PublishTriviaWrongAnswer_OverRealRabbitMq_RelaysToNotifier()
    {
        var sessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var blockedOptionId = Guid.NewGuid();

        await fixture.PublishAsync(new TriviaWrongAnswerIntegrationEvent(
            sessionId, teamId, StageOrder: 1, BlockedOptionId: blockedOptionId,
            AttemptsUsed: 1, MaxAttempts: 3, ScoreChange: -10, NewScore: 90, ParticipantName: "Ana"));

        var call = await PollAsync(
            () => Task.FromResult(fixture.NotifierSpy.TriviaWrongAnswerCalls
                .FirstOrDefault(c => c.SessionId == sessionId && c.TeamId == teamId)),
            c => c is not null);

        call.Should().NotBeNull();
        call!.BlockedOptionId.Should().Be(blockedOptionId);
        call.AttemptsUsed.Should().Be(1);
    }

    [Fact]
    public async Task PublishClueReleased_OverRealRabbitMq_RelaysToNotifier()
    {
        var sessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        await fixture.PublishAsync(new ClueReleasedIntegrationEvent(
            sessionId, teamId, Content: "Busca cerca del reloj", Latitude: 10.5, Longitude: -66.9,
            RadiusMeters: 30, ClueNumber: 2, IsAutomatic: false));

        var call = await PollAsync(
            () => Task.FromResult(fixture.NotifierSpy.ClueReleasedCalls
                .FirstOrDefault(c => c.SessionId == sessionId && c.TeamId == teamId)),
            c => c is not null);

        call.Should().NotBeNull();
        call!.Content.Should().Be("Busca cerca del reloj");
        call.ClueNumber.Should().Be(2);
        call.IsAutomatic.Should().BeFalse();
    }
}
