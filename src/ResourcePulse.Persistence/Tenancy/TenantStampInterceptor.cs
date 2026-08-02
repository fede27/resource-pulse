using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ResourcePulse.Common.Tenancy;

namespace ResourcePulse.Persistence.Tenancy;

/// <summary>
/// Stamps the shadow <c>TenantId</c> on every inserted row (ADR-0029).
/// <para>
/// Writing without a resolved tenant is a hard failure, not a default: a row with
/// no tenant would be invisible to every tenant and would violate the RLS
/// <c>WITH CHECK</c> anyway. Failing here turns a silent data-corruption bug into
/// an obvious one.
/// </para>
/// </summary>
public sealed class TenantStampInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Metadata.FindProperty(TenantModel.TenantIdProperty) is null)
                continue;

            var property = entry.Property(TenantModel.TenantIdProperty);

            switch (entry.State)
            {
                case EntityState.Added:
                    if (!tenantContext.IsResolved)
                        throw new InvalidOperationException(
                            $"Cannot insert {entry.Metadata.DisplayName()}: no tenant is resolved for the " +
                            "current operation. Requests derive the tenant from the validated token; " +
                            "background work must open a TenantScope explicitly.");

                    property.CurrentValue = tenantContext.TenantId;
                    break;

                case EntityState.Modified:
                    // A row never migrates between tenants. Re-pointing an
                    // organization is a control-plane operation on the registry,
                    // not a rewrite of planning data.
                    property.IsModified = false;
                    break;
            }
        }
    }
}
