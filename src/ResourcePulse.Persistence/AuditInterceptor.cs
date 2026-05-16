using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ResourcePulse.Common.Auth;
using ResourcePulse.Domain;

namespace ResourcePulse.Persistence;

public sealed class AuditInterceptor(ICurrentUserAccessor currentUserAccessor) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAudit(DbContext? context)
    {
        if (context is null) return;

        var sub = currentUserAccessor.User.Sub;
        if (string.IsNullOrEmpty(sub))
            throw new InvalidOperationException(
                "Cannot persist changes: current user Sub is empty. Ensure authentication is configured.");

        var now = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = sub;
                entry.Entity.UpdatedAt = null;
                entry.Entity.UpdatedBy = null;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = sub;
                // Prevent silent overwrites — these must never change after creation
                entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
            }
        }
    }
}
