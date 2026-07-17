namespace UMBRAL_Back_end.IntegrationTests.SyncHealth;

using System.Net;
using FluentAssertions;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using Xunit;

/// <summary>
/// Gap #3 — los controllers internos de sync-health (Clue/Mission/Stage/Team) no
/// tenían ningún test. Su GET expone el estado de las réplicas locales; acá se
/// verifica que respondan 200 contra Postgres real (el POST /reproject llama a
/// otros servicios, así que queda fuera del alcance de un test en aislamiento).
/// Una clase por servicio porque cada uno vive en su propia colección/fixture.
/// </summary>
public class InternalSyncHealthTests
{
    private const string Route = "/api/internal/sync-health";

    [Collection(ClueServiceCollection.Name)]
    public class ClueInternalSyncHealth(ClueServicePostgresFixture fixture)
    {
        [Fact]
        public async Task Get_ReturnsOk()
        {
            var response = await fixture.Factory.CreateClient().GetAsync(Route);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Collection(MissionServiceCollection.Name)]
    public class MissionInternalSyncHealth(MissionServicePostgresFixture fixture)
    {
        [Fact]
        public async Task Get_ReturnsOk()
        {
            var response = await fixture.Factory.CreateClient().GetAsync(Route);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Collection(StageServiceCollection.Name)]
    public class StageInternalSyncHealth(StageServicePostgresFixture fixture)
    {
        [Fact]
        public async Task Get_ReturnsOk()
        {
            var response = await fixture.Factory.CreateClient().GetAsync(Route);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Collection(TeamServiceCollection.Name)]
    public class TeamInternalSyncHealth(TeamServicePostgresFixture fixture)
    {
        [Fact]
        public async Task Get_ReturnsOk()
        {
            var response = await fixture.Factory.CreateClient().GetAsync(Route);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
