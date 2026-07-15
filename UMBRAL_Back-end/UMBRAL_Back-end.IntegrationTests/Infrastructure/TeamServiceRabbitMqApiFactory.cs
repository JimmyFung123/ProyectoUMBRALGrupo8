// TeamService generates its own top-level-statements `Program` class in the *global*
// namespace, same conflict handled elsewhere in this project (see
// TeamServiceApiFactory.cs / ClueServiceRabbitMqApiFactory.cs). Needed here directly
// because this factory wires TeamService's TeamsDbContext.
extern alias TeamServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using TeamServiceAssembly::TeamService.Infrastructure.Persistence;
using TeamProgram = TeamServiceAssembly::Program;

/// <summary>Level-2: TeamService wired to a real Testcontainers RabbitMQ broker.</summary>
public class TeamServiceRabbitMqApiFactory(string connectionString, string rabbitMqConnectionString)
    : RabbitMqBackedApiFactory<TeamProgram, TeamsDbContext>(connectionString, rabbitMqConnectionString);
