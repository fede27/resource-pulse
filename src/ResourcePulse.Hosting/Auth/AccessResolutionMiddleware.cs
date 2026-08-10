using ResourcePulse.Services.Access;

namespace ResourcePulse.Hosting.Auth;

/// <summary>
/// Resolves the caller's membership in the current tenant and deposits it for the
/// authorization policies to read (ADR-0030).
/// </summary>
/// <remarks>
/// <para>
/// <b>It resolves; it does not reject.</b> Turning "not a member" into a 403 here
/// would also close <c>GET /api/me</c>, and the client needs that endpoint to
/// answer precisely when the answer is "you have no access": a bare 403 on the one
/// call that would explain the situation leaves the SPA with a blank screen and
/// nothing to say. Rejection is the policies' job, and they are applied per
/// endpoint.
/// </para>
/// <para>
/// <b>Ordering is a correctness requirement, not a preference.</b> It must run
/// after <see cref="TenantResolutionMiddleware"/>, because the membership table is
/// tenant-scoped and RLS-protected: with no tenant in the Postgres session the
/// read returns nothing and every caller silently becomes a non-member.
/// </para>
/// </remarks>
public sealed class AccessResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAccessResolver resolver)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var resolution = await resolver.ResolveAsync(context.RequestAborted);

            if (resolution is { IsMember: true, Role: { } role })
                HttpContextCurrentAccess.Set(context, role);
        }

        await next(context);
    }
}

public static class AccessResolutionMiddlewareExtensions
{
    /// <summary>
    /// Must be registered AFTER <c>UseTenantResolution</c> — see the remarks on
    /// <see cref="AccessResolutionMiddleware"/> — and BEFORE <c>UseAuthorization</c>.
    /// </summary>
    public static IApplicationBuilder UseAccessResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<AccessResolutionMiddleware>();
}
