namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using SessionService.Domain.Sessions;
using Xunit;

public class SessionEventTests
{
    // ── Creación ──────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidData_SetsAllFieldsCorrectly()
    {
        var sessionId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var evt = SessionEvent.Create(sessionId, "La sesión fue iniciada");

        evt.Id.Should().NotBeEmpty();
        evt.SessionId.Should().Be(sessionId);
        evt.Description.Should().Be("La sesión fue iniciada");
        evt.OccurredAt.Should().BeOnOrAfter(before);
        evt.OccurredAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void Create_WithValidData_AssignsUniqueIds()
    {
        var sessionId = Guid.NewGuid();

        var evt1 = SessionEvent.Create(sessionId, "Evento 1");
        var evt2 = SessionEvent.Create(sessionId, "Evento 2");

        evt1.Id.Should().NotBe(evt2.Id);
    }

    [Fact]
    public void Create_OccurredAt_IsUtc()
    {
        var evt = SessionEvent.Create(Guid.NewGuid(), "Evento de prueba");

        evt.OccurredAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Create_PreservesSessionId()
    {
        var sessionId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        var evt1 = SessionEvent.Create(sessionId, "Evento A");
        var evt2 = SessionEvent.Create(otherId, "Evento B");

        evt1.SessionId.Should().Be(sessionId);
        evt2.SessionId.Should().Be(otherId);
        evt1.SessionId.Should().NotBe(evt2.SessionId);
    }

    [Theory]
    [InlineData("La sesión fue iniciada")]
    [InlineData("Equipo Alfa avanzó a la etapa 3")]
    [InlineData("Se envió una pista al Equipo Beta")]
    public void Create_WithDifferentDescriptions_PreservesExactText(string description)
    {
        var evt = SessionEvent.Create(Guid.NewGuid(), description);

        evt.Description.Should().Be(description);
    }

    // ── HU-22: ActorName ──────────────────────────────────────────────────────

    [Fact]
    public void Create_WithoutActorName_DefaultsToSistema()
    {
        var evt = SessionEvent.Create(Guid.NewGuid(), "Evento automático");

        evt.ActorName.Should().Be(SessionEvent.SystemActor);
        evt.ActorName.Should().Be("Sistema");
    }

    [Fact]
    public void Create_WithActorName_StoresExactValue()
    {
        var evt = SessionEvent.Create(Guid.NewGuid(), "Pista liberada", actorName: "Prof. Ortega");

        evt.ActorName.Should().Be("Prof. Ortega");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithBlankActorName_FallsBackToSistema(string? actorName)
    {
        var evt = SessionEvent.Create(Guid.NewGuid(), "Evento", actorName: actorName);

        evt.ActorName.Should().Be("Sistema");
    }

    [Fact]
    public void Create_TrimsActorName()
    {
        var evt = SessionEvent.Create(Guid.NewGuid(), "Evento", actorName: "  Operador X  ");

        evt.ActorName.Should().Be("Operador X");
    }
}
