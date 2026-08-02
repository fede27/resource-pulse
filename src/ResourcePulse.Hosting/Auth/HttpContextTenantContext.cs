using ResourcePulse.Common.Tenancy;

namespace ResourcePulse.Hosting.Auth;

/// <summary>
/// Request-scoped tenant, read through a singleton.
/// </summary>
/// <remarks>
/// Singleton lifetime is required, not preferred: pooled <c>DbContext</c>
/// instances are resolved from the root provider, so anything they depend on
/// must be a singleton. Like <see cref="HttpContextCurrentUserAccessor"/>, this
/// stays correct because it resolves the ambient request at call time rather
/// than capturing state at construction.
/// <para>
/// Outside a request (dev seeding, provisioning) it falls back to an explicit
/// <see cref="TenantScope"/>. There is deliberately no third source.
/// </para>
/// </remarks>
public sealed class HttpContextTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    private const string ItemKey = "ResourcePulse.TenantId";

    internal static void Set(HttpContext context, Guid tenantId) =>
        context.Items[ItemKey] = tenantId;

    public bool IsResolved => Resolve() is not null;

    public Guid TenantIdOrEmpty => Resolve() ?? Guid.Empty;

    public Guid TenantId =>
        Resolve() ?? throw new InvalidOperationException(
            "No tenant is resolved for the current operation. Requests derive the tenant from " +
            "the validated token; background work must open a TenantScope explicitly.");

    private Guid? Resolve()
    {
        if (httpContextAccessor.HttpContext?.Items.TryGetValue(ItemKey, out var value) == true &&
            value is Guid tenantId)
            return tenantId;

        return TenantScope.CurrentTenantId;
    }
}
