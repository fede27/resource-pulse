namespace ResourcePulse.Common.Tenancy;

/// <summary>
/// Tenant context for processes that have no requests at all — the signal worker,
/// and anything else that runs a unit of work per tenant on its own schedule.
/// </summary>
/// <remarks>
/// <para>
/// It reads <see cref="TenantScope"/> and nothing else. That is the whole point:
/// out here there is no token to derive a tenant from, so the ONLY sanctioned
/// source is the explicit, noisy scope — and unresolved must stay unresolved
/// rather than fall back to anything.
/// </para>
/// <para>
/// <see cref="TenantIdOrEmpty"/> degrading to <see cref="Guid.Empty"/> is
/// fail-close by construction: no row carries an empty tenant, so a unit of work
/// that forgot to open a scope reads an empty database instead of another
/// tenant's.
/// </para>
/// </remarks>
public sealed class AmbientTenantContext : ITenantContext
{
    public bool IsResolved => TenantScope.CurrentTenantId is not null;

    public Guid TenantIdOrEmpty => TenantScope.CurrentTenantId ?? Guid.Empty;

    public Guid TenantId =>
        TenantScope.CurrentTenantId ?? throw new InvalidOperationException(
            "No tenant scope is open. Background work must wrap each unit of work in " +
            "TenantScope.For(tenantId) before touching tenant data.");
}
