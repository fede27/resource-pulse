using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using ResourcePulse.Services.Identity.Login;

namespace ResourcePulse.Http.Auth;

/// <summary>
/// Carries a password-verified Zitadel session between the two steps of the
/// mandatory-password-change flow (ADR-0031).
/// </summary>
/// <remarks>
/// <para>
/// The session token is a bearer credential for an authenticated session, so it
/// must not be handed to the page and posted back: it lives in an <b>http-only,
/// same-site, data-protected</b> cookie that JavaScript cannot read.
/// </para>
/// <para>
/// It is deliberately <b>short-lived and path-scoped</b> to <c>/api/auth</c>: the
/// only thing it may ever authorise is finishing the sign-in it came from.
/// </para>
/// </remarks>
public sealed class PendingLoginSessionCookie(IDataProtectionProvider provider)
{
    public const string CookieName = "rp_login_session";

    /// <summary>Long enough to choose a new password, short enough not to be a standing credential.</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly IDataProtector protector = provider.CreateProtector("ResourcePulse.PendingLoginSession.v1");

    public void Write(HttpResponse response, PendingLoginSession session)
    {
        var payload = protector.Protect(JsonSerializer.Serialize(session));

        response.Cookies.Append(CookieName, payload, new CookieOptions
        {
            HttpOnly = true,
            // The page and the API are same-origin (the dev server proxies /api),
            // so Strict costs nothing and removes the cross-site case entirely.
            SameSite = SameSiteMode.Strict,
            // Not pinned to true: development runs the whole stack over plain HTTP
            // (ADR-0029), and a Secure cookie would simply never be stored there.
            Secure = response.HttpContext.Request.IsHttps,
            Path = "/api/auth",
            MaxAge = Lifetime,
            IsEssential = true
        });
    }

    /// <summary>The pending session, or <c>null</c> when absent, tampered with, or expired.</summary>
    public PendingLoginSession? Read(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(CookieName, out var payload) || string.IsNullOrEmpty(payload))
            return null;

        try
        {
            return JsonSerializer.Deserialize<PendingLoginSession>(protector.Unprotect(payload));
        }
        catch (Exception e) when (e is System.Security.Cryptography.CryptographicException or JsonException)
        {
            // An unreadable cookie is indistinguishable from no cookie: the caller
            // starts the sign-in again. Nothing here is worth a 500.
            return null;
        }
    }

    public void Clear(HttpResponse response) =>
        response.Cookies.Delete(CookieName, new CookieOptions { Path = "/api/auth" });
}
