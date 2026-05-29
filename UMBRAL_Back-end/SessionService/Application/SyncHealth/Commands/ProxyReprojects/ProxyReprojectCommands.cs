namespace SessionService.Application.SyncHealth.Commands.ProxyReprojects;

using MediatR;
using SessionService.Application.SyncHealth.Commands.ReprojectSessionMissionsLookup;

// HU-27 — thin commands that forward a reproject request to the downstream
// service that owns the projection. The aggregator endpoint never touches
// other services' tables directly; it always asks the owner to rebuild.

public record ReprojectStageMissionsLookupCommand() : IRequest<ReprojectActionResultDto>;
public record ReprojectStageCountLookupCommand() : IRequest<ReprojectActionResultDto>;
public record ReprojectStageLookupCommand() : IRequest<ReprojectActionResultDto>;
public record ReprojectRankingForSessionCommand(Guid SessionId) : IRequest<ReprojectActionResultDto>;
