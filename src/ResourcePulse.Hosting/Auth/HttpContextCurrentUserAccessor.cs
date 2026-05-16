using System.Security.Claims;
using ResourcePulse.Common.Auth;

namespace ResourcePulse.Hosting.Auth;

public sealed class HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public CurrentUser User => IsAuthenticated ? MapUser() : CurrentUser.Anonymous;

    private CurrentUser MapUser()
    {
        var principal = httpContextAccessor.HttpContext!.User;
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var email = principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var name = principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

        var knownTypes = new HashSet<string>
        {
            ClaimTypes.NameIdentifier,
            ClaimTypes.Email,
            ClaimTypes.Name
        };

        var extraClaims = principal.Claims
            .Where(c => !knownTypes.Contains(c.Type))
            .ToDictionary(c => c.Type, c => c.Value);

        return new CurrentUser(sub, email, name, extraClaims);
    }
}
