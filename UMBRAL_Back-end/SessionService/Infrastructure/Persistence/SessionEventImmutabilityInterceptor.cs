namespace SessionService.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SessionService.Domain.Sessions;

/// <summary>
/// HU-26 criterion 2 — the command audit log is append-only.
///
/// Rejects any <see cref="EntityState.Modified"/> or
/// <see cref="EntityState.Deleted"/> change tracked against
/// <see cref="SessionEvent"/> before the change is sent to the database.
///
/// This is the last line of defense; the entity already has private setters
/// and the repository exposes no Update/Delete method. The interceptor closes
/// the gap for any future code path that might accidentally mutate a tracked
/// instance, and gives a clear error message to whoever tries.
/// </summary>
public class SessionEventImmutabilityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        EnforceImmutability(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        EnforceImmutability(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void EnforceImmutability(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries<SessionEvent>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "SessionEvent rows are immutable (HU-26). " +
                    $"Attempted state '{entry.State}' on SessionEvent {entry.Entity.Id} was rejected.");
            }
        }
    }
}
