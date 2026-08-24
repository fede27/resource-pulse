namespace ResourcePulse.Http.Auth;

/// <summary>
/// Names the rate-limit policy guarding the anonymous login endpoints (ADR-0031).
/// </summary>
/// <remarks>
/// Named here, next to the controller that consumes it, and configured in
/// <c>ResourcePulse.Hosting</c> — the same split as <see cref="AccessPolicies"/>.
/// <para>
/// It is not redundant with Zitadel's own account lockout. Lockout protects one
/// account against many guesses; this protects the <b>instance</b> against one
/// caller spraying one password across many accounts, which never trips a
/// per-account counter. Until this endpoint existed the application had no
/// unauthenticated surface at all, so it also had nothing to throttle.
/// </para>
/// </remarks>
public static class LoginRateLimiting
{
    public const string PolicyName = "login";
}
