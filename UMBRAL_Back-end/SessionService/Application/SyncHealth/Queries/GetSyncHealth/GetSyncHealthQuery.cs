namespace SessionService.Application.SyncHealth.Queries.GetSyncHealth;

using MediatR;

/// <summary>
/// HU-27 — global sync-health snapshot. Aggregates every CQRS read model
/// across the system into a single response so the admin dashboard can render
/// one card per projection with its lag / drift status.
/// </summary>
public record GetSyncHealthQuery() : IRequest<SyncHealthDto>;
