namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using SessionService.Domain.Sessions;
using Xunit;

public class ActorNameResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WhenActorNameIsEmpty_FallsBackToSystemActor(string? actorName)
        => ActorNameResolver.Resolve(actorName).Should().Be(SessionEvent.SystemActor);

    [Fact]
    public void Resolve_WhenActorNameProvided_ReturnsItTrimmed()
        => ActorNameResolver.Resolve("  Prof. Ramírez  ").Should().Be("Prof. Ramírez");
}
