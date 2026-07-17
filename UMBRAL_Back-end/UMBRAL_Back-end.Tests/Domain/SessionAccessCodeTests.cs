namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using SessionService.Domain.Sessions;
using Xunit;

public class SessionAccessCodeTests
{
    [Fact]
    public void Create_GeneratesNonEmptyAccessCode()
    {
        var session = Session.Create(Guid.NewGuid(), "Test Session").Value;
        session.AccessCode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Create_AccessCodeHasCorrectLength()
    {
        var session = Session.Create(Guid.NewGuid(), "Test Session").Value;
        session.AccessCode.Should().HaveLength(6);
    }

    [Fact]
    public void Create_AccessCodeIsUppercase()
    {
        var session = Session.Create(Guid.NewGuid(), "Test Session").Value;
        session.AccessCode.Should().Be(session.AccessCode.ToUpperInvariant());
    }

    [Fact]
    public void Create_TwoSessions_HaveDifferentCodes()
    {
        // Statistical: probability of collision is 1/36^6 ≈ negligible
        var s1 = Session.Create(Guid.NewGuid(), "Session 1").Value;
        var s2 = Session.Create(Guid.NewGuid(), "Session 2").Value;
        s1.AccessCode.Should().NotBe(s2.AccessCode);
    }
}
