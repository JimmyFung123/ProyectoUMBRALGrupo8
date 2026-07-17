// StageService generates its own top-level-statements `Program` class in the *global*
// namespace, same conflict as TeamService/ClueService (see the .csproj comment on the
// StageService ProjectReference). Its ProjectReference carries Aliases=StageServiceAssembly
// metadata so this file resolves its Program via that extern alias instead of the bare
// (Mission's) `Program`.
extern alias StageServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using StageServiceAssembly::StageService.Infrastructure.Persistence;
using StageProgram = StageServiceAssembly::Program;

public class StageServiceApiFactory(string connectionString)
    : IntegrationTestApiFactory<StageProgram, StagesDbContext>(connectionString);
