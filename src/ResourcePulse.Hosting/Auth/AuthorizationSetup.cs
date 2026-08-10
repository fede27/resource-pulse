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

            // STEP 1 of ADR-0030 deliberately stops here: the fallback still only
            // requires authentication, so no existing endpoint changes behaviour
            // and this whole slice is observable but inert. Step 2 raises it to
            // the Viewer policy, at which point membership becomes mandatory
            // everywhere except the endpoints explicitly exempted.
            opts.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
    }
}
