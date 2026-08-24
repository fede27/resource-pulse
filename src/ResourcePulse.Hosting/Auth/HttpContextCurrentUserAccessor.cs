using System.Security.Claims;
using ResourcePulse.Common.Auth;

namespace ResourcePulse.Hosting.Auth;

public sealed class HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public CurrentUser User => IsAuthenticated ? MapUser() : CurrentUser.Anonymous;

    public string? AuthenticationScheme =>
        IsAuthenticated ? httpContextAccessor.HttpContext!.User.Identity!.AuthenticationType : null;

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

        // A claim type can legitimately repeat, and a real Zitadel access token
        // proves it: `aud` carries the project id AND both application client ids,
        // so a naive ToDictionary threw "An item with the same key has already
        // been added. Key: aud" and every authenticated request became a 500.
        // FakeAuth never hits it — it issues one claim per type — which is how
        // this survived until the Zitadel provider was switched on.
        //
        // The bag is diagnostic (it feeds auditing and request logging), so the
        // repeated values are JOINED rather than one of them silently winning:
        // dropping two of three audiences is exactly the sort of thing you want to
        // see in a log, not guess at.
        var extraClaims = principal.Claims
            .Where(c => !knownTypes.Contains(c.Type))
            .GroupBy(c => c.Type, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => string.Join(',', g.Select(c => c.Value)), StringComparer.Ordinal);

        return new CurrentUser(sub, email, name, extraClaims);
    }
}
