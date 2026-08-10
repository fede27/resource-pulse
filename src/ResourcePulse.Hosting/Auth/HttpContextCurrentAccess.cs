using ResourcePulse.Domain.Access;
using ResourcePulse.Services.Access;

namespace ResourcePulse.Hosting.Auth;

/// <summary>
/// The membership resolved for the current request, deposited by
/// <see cref="AccessResolutionMiddleware"/>.
/// </summary>
/// <remarks>
/// Unlike <see cref="HttpContextTenantContext"/> and
/// <see cref="HttpContextCurrentUserAccessor"/>, this one carries no lifetime
/// constraint: nothing in the pooled <c>DbContext</c>'s dependency graph reads it,
/// so it is registered scoped like any ordinary request service.
/// </remarks>
public sealed class HttpContextCurrentAccess(IHttpContextAccessor httpContextAccessor) : ICurrentAccess
{
    private const string ItemKey = "ResourcePulse.AccessRole";

    internal static void Set(HttpContext context, AppRole role) =>
        context.Items[ItemKey] = role;

    public AppRole? Role =>
        httpContextAccessor.HttpContext?.Items.TryGetValue(ItemKey, out var value) == true &&
        value is AppRole role
            ? role
            : null;

    public bool IsMember => Role is not null;

    // The enum's order IS the hierarchy (see AppRole), so containment is a
    // comparison and never a lookup table that could drift away from it.
    public bool Has(AppRole required) => Role is { } role && role >= required;
}
