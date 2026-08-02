namespace ResourcePulse.Common.Tenancy;

/// <summary>
/// An explicit, ambient tenant for work that runs <i>outside</i> a request — dev
/// seeding, migrations, background provisioning. It is the only sanctioned way to
/// obtain a tenant context without a validated token, and it is deliberately
/// noisy at the call site: <c>using var _ = TenantScope.For(id);</c>.
/// </summary>
/// <remarks>
/// This is not a back door for request handling. Request-scoped tenancy always
/// comes from the token via the control-plane registry; an ambient override must
/// never be set from user-controlled input.
/// </remarks>
public static class TenantScope
{
    private static readonly AsyncLocal<Guid?> Current = new();

    public static Guid? CurrentTenantId => Current.Value;

    public static IDisposable For(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant scope requires a non-empty tenant id.", nameof(tenantId));

        var previous = Current.Value;
        Current.Value = tenantId;
        return new Restore(previous);
    }

    private sealed class Restore(Guid? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Current.Value = previous;
        }
    }
}
