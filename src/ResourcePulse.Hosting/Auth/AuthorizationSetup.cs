using Microsoft.AspNetCore.Authorization;
using ResourcePulse.Domain.Access;
using ResourcePulse.Http.Auth;
using ResourcePulse.Services.Access;

namespace ResourcePulse.Hosting.Auth;

/// <summary>
/// Requires a minimum application role, satisfied by that role or any above it.
/// </summary>
public sealed class MinimumRoleRequirement(AppRole minimum) : IAuthorizationRequirement
{
    public AppRole Minimum { get; } = minimum;
}

/// <summary>
/// Evaluates <see cref="MinimumRoleRequirement"/> against OUR membership store,
/// never against a token claim (ADR-0029 §6, ADR-0030).
/// </summary>
/// <remarks>
/// This is why the codebase has no <c>[Authorize(Roles = ...)]</c>: a claim-based
/// check would be satisfied by anything an identity provider chose to assert, and
/// the guarantee that application roles are ours would rest on nobody ever writing
/// that attribute. Here the guarantee is structural — the handler cannot read a
/// claim because it never looks at one.
/// </remarks>
public sealed class MinimumRoleHandler(ICurrentAccess access)
    : AuthorizationHandler<MinimumRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MinimumRoleRequirement requirement)
    {
        if (access.Has(requirement.Minimum))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

public static class AuthorizationSetup
{
    public static void AddResourcePulseAuthorization(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ICurrentAccess, HttpContextCurrentAccess>();
        builder.Services.AddScoped<IAuthorizationHandler, MinimumRoleHandler>();

        builder.Services.AddAuthorization(opts =>
        {
            opts.AddPolicy(AccessPolicies.Viewer, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new MinimumRoleRequirement(AppRole.Viewer)));

            opts.AddPolicy(AccessPolicies.Planner, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new MinimumRoleRequirement(AppRole.Planner)));

            opts.AddPolicy(AccessPolicies.Owner, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new MinimumRoleRequirement(AppRole.Owner)));

            // The floor for the whole API: every endpoint without its own
            // authorization metadata requires a MEMBERSHIP, not merely a valid
            // token. Authentication alone used to be the fallback, which meant an
            // authenticated stranger with a resolved tenant could write — the
            // fail-close only ever bound where a policy happened to be attached.
            //
            // Consequence to keep in mind when adding an endpoint: reads need no
            // attribute (they are Viewer by falling through here), writes must be
            // annotated. An un-annotated write is a Viewer-writable endpoint.
            opts.FallbackPolicy = opts.GetPolicy(AccessPolicies.Viewer);
        });
    }
}
