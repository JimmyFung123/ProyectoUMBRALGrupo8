namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using SessionService.Domain.Sessions;
using Xunit;

public class SessionOwnershipTests
{
    [Fact]
    public void Create_WithOperatorId_StoresCreatedByOperatorId()
    {
        var session = Session.Create(Guid.NewGuid(), "Sesión de prueba", null, "keycloak-sub-123").Value;
        session.CreatedByOperatorId.Should().Be("keycloak-sub-123");
    }

    [Fact]
    public void Create_WithoutOperatorId_LeavesCreatedByOperatorIdNull()
    {
        // Backward compatibility: sessions created before RB-10 (or via any
        // caller that omits the operator) have no recorded owner.
        var session = Session.Create(Guid.NewGuid(), "Sesión de prueba").Value;
        session.CreatedByOperatorId.Should().BeNull();
    }
}
