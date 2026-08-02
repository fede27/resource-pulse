using ResourcePulse.Common.Tenancy;
using ResourcePulse.Services.Tenancy;

namespace ResourcePulse.Hosting.Auth;

/// <summary>
/// Turns a validated principal into a tenant context (ADR-0029).
/// </summary>
/// <remarks>
/// <para>
/// The tenant is derived <b>only</b> from the token, resolved through the
/// control-plane registry. Nothing here reads a header, a route value or the
/// request host — those are attacker-controlled and would make tenant selection
/// a client decision.
/// </para>
/// <para>
/// Every failure to resolve is a <b>403</b>, not a fallback: an authenticated
/// user whose organization we do not recognise has no business seeing any
/// tenant's data. 403 rather than 401 because the identity is fine — the
/// authorization to reach a tenant is what is missing, and re-authenticating
/// would not change the outcome.
/// </para>
/// </remarks>
public sealed class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ITenantResolver resolver)
    {
        // Anonymous endpoints (health checks, swagger) never reach a tenant-scoped
        // store; letting them through unresolved keeps the query filter and the
        // RLS policies as the backstop.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var resolution = await resolver.ResolveAsync(context.User, context.RequestAborted);

        if (!resolution.IsResolved)
        {
            logger.LogWarning(
                "Tenant resolution failed ({Outcome}) for organization {OrganizationId}; rejecting request to {Path}.",
                resolution.Outcome, resolution.OrganizationId ?? "<none>", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                title = "Tenant not resolved",
                status = StatusCodes.Status403Forbidden,
                detail = "The authenticated organization is not mapped to an active tenant."
            });
            return;
        }

        HttpContextTenantContext.Set(context, resolution.TenantId);
        await next(context);
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    /// <summary>
    /// Must be registered AFTER <c>UseAuthentication</c> — it reads the validated
    /// principal — and BEFORE anything that touches the tenant-scoped store.
    /// </summary>
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();
}
