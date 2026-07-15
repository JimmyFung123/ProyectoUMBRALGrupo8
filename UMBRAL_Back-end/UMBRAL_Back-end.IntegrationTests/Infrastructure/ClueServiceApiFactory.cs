// ClueService generates its own top-level-statements `Program` class in the *global*
// namespace, same conflict as TeamService (see the .csproj comment on the ClueService
// ProjectReference). Its ProjectReference carries Aliases=ClueServiceAssembly metadata
// so this file resolves its Program via that extern alias instead of the bare
// (Mission's) `Program`.
extern alias ClueServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using ClueServiceAssembly::ClueService.Infrastructure.Persistence;
using ClueProgram = ClueServiceAssembly::Program;

public class ClueServiceApiFactory(string connectionString)
    : IntegrationTestApiFactory<ClueProgram, CluesDbContext>(connectionString);
