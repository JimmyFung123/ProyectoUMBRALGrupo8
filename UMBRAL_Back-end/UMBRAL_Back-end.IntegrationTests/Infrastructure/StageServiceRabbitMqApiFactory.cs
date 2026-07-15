// StageService generates its own top-level-statements `Program` class in the *global*
// namespace, same conflict handled elsewhere in this project (see
// StageServiceApiFactory.cs / ClueServiceRabbitMqApiFactory.cs). Needed here directly
// because this factory wires StageService's StagesDbContext.
extern alias StageServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using StageServiceAssembly::StageService.Infrastructure.Persistence;
using StageProgram = StageServiceAssembly::Program;

/// <summary>Level-2: StageService wired to a real Testcontainers RabbitMQ broker.</summary>
public class StageServiceRabbitMqApiFactory(string connectionString, string rabbitMqConnectionString)
    : RabbitMqBackedApiFactory<StageProgram, StagesDbContext>(connectionString, rabbitMqConnectionString);
