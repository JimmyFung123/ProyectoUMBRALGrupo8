namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using SessionService.Domain.Sessions;
using Xunit;

public class SessionAvailabilityPolicyTests
{
    [Theory]
    [InlineData(SessionStatus.InProgress, true)]
    [InlineData(SessionStatus.Pending, false)]
    [InlineData(SessionStatus.Paused, false)]
    [InlineData(SessionStatus.Completed, false)]
    [InlineData(SessionStatus.Cancelled, false)]
    public void IsInProgress_ReturnsExpected(SessionStatus status, bool expected)
        => SessionAvailabilityPolicy.IsInProgress(status).Should().Be(expected);

    [Theory]
    [InlineData(SessionStatus.InProgress, true)]
    [InlineData(SessionStatus.Paused, true)]
    [InlineData(SessionStatus.Pending, false)]
    [InlineData(SessionStatus.Completed, false)]
    [InlineData(SessionStatus.Cancelled, false)]
    public void AcceptsOperatorMessage_ReturnsExpected(SessionStatus status, bool expected)
        => SessionAvailabilityPolicy.AcceptsOperatorMessage(status).Should().Be(expected);
}
