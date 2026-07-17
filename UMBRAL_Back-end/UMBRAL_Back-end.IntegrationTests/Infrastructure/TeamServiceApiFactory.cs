// TeamService and MissionService each generate a top-level-statements `Program`
// class in the *global* namespace. Referencing both assemblies unqualified causes
// CS0433 ("Program exists in both..."), so TeamService's ProjectReference carries
// an Aliases=TeamServiceAssembly metadata (see the .csproj) and this file resolves
// its Program via that extern alias instead of the bare (Mission's) `Program`.
extern alias TeamServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using TeamServiceAssembly::TeamService.Infrastructure.Persistence;
using TeamProgram = TeamServiceAssembly::Program;

public class TeamServiceApiFactory(string connectionString)
    : IntegrationTestApiFactory<TeamProgram, TeamsDbContext>(connectionString);
