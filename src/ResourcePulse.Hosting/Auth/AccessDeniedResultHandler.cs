using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using ResourcePulse.Services.Access;

namespace ResourcePulse.Hosting.Auth;

/// <summary>
/// Gives an authorization failure a body worth reading.
/// </summary>
/// <remarks>
/// ASP.NET Core's default handler answers a failed policy with a bare 403 and no
/// payload, which leaves the client unable to distinguish "you are not a member of
/// this tenant" (talk to an owner) from "you are, but this action needs a higher
/// role" (expected, and the UI should have hidden the gesture). Both are emitted
/// here with a distinct <c>type</c>; everything else is delegated untouched.
/// </remarks>
public sealed class AccessDeniedResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        var isRoleFailure =
            authorizeResult.Forbidden &&
            policy.Requirements.OfType<MinimumRoleRequirement>().Any();

        if (!isRoleFailure)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        // Resolved from the request, not injected: the authorization middleware
        // holds this handler as a SINGLETON, so a constructor-injected scoped
        // ICurrentAccess fails DI validation at startup (and would be a captive
        // dependency if validation were off).
        var access = context.RequestServices.GetRequiredService<ICurrentAccess>();

        var problem = access.IsMember
            ? AuthProblems.Create(
                AuthProblems.InsufficientRole,
                "Insufficient role",
                "Your role in this tenant does not allow this action.")
            : AuthProblems.Create(
                AuthProblems.NotAMember,
                "Not a member",
                "You are authenticated, but no membership grants you access to this tenant.");

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
