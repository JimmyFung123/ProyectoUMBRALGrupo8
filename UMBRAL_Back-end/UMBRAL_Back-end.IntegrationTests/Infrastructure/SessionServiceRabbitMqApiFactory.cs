// SessionService generates its own top-level-statements `Program` class in the *global*
// namespace, same conflict handled elsewhere in this project (see
// SessionServiceApiFactory.cs / ClueServiceRabbitMqApiFactory.cs). Needed here directly
// because this factory wires SessionService's SessionsDbContext.
extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using SessionServiceAssembly::SessionService.Infrastructure.Persistence;
using SessionProgram = SessionServiceAssembly::Program;

/// <summary>Level-2: SessionService wired to a real Testcontainers RabbitMQ broker.</summary>
public class SessionServiceRabbitMqApiFactory(string connectionString, string rabbitMqConnectionString)
    : RabbitMqBackedApiFactory<SessionProgram, SessionsDbContext>(connectionString, rabbitMqConnectionString);
