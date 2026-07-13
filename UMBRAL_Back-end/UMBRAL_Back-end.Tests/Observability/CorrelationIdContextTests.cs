namespace UMBRAL_Back_end.Tests.Observability;

using FluentAssertions;
using UMBRAL.Observability;
using Xunit;

/// <summary>
/// Portador ambiental del correlation id: base de la propagación entre servicios.
/// </summary>
public class CorrelationIdContextTests
{
    [Fact]
    public void Current_WhenReset_IsNull()
    {
        CorrelationIdContext.Current = null;
        CorrelationIdContext.Current.Should().BeNull();
    }

    [Fact]
    public void GetOrCreate_WhenUnset_GeneratesAndStores()
    {
        CorrelationIdContext.Current = null;

        var id = CorrelationIdContext.GetOrCreate();

        id.Should().NotBeNullOrWhiteSpace();
        CorrelationIdContext.Current.Should().Be(id);
    }

    [Fact]
    public void GetOrCreate_WhenAlreadySet_ReturnsExisting()
    {
        CorrelationIdContext.Current = "fixed-id";

        CorrelationIdContext.GetOrCreate().Should().Be("fixed-id");
    }
}
