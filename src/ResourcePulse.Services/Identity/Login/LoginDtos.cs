namespace ResourcePulse.Services.Identity.Login;

/// <summary>
/// What the login page must do next (ADR-0031).
/// </summary>
/// <remarks>
/// Authenticating is not one step but a small state machine, and the page has to
/// be told which state it is in. Collapsing these into "ok / not ok" is what turns
/// the very first sign-in of a real Zitadel user — who is created with
/// <c>passwordChangeRequired</c> — into a dead end.
/// </remarks>
public enum LoginStep
{
    /// <summary>The authorization request is finalised; follow <see cref="LoginResultDto.CallbackUrl"/>.</summary>
    Completed = 0,

    /// <summary>The password was correct but must be replaced before the session counts.</summary>
    PasswordChangeRequired,

    /// <summary>
    /// The user carries a factor this page does not implement (passkey, TOTP,
    /// OTP). Honest dead end with an explanation rather than a silent failure.
    /// </summary>
    AdditionalFactorRequired
}

/// <summary>The public half of a sign-in attempt — everything safe to send to the browser.</summary>
public sealed record LoginResultDto
{
    public required LoginStep Step { get; init; }

    /// <summary>
    /// Zitadel's callback URL, carrying the authorization code. Populated only on
    /// <see cref="LoginStep.Completed"/>; navigating to it re-enters the ordinary
    /// PKCE flow, which is why nothing downstream of the redirect had to change.
    /// </summary>
    public string? CallbackUrl { get; init; }
}

/// <summary>
/// A password check that succeeded but has not yet been exchanged for a callback.
/// </summary>
/// <remarks>
/// This never reaches the browser as-is: the session token is a bearer credential
/// for the authenticated session. It is carried between the two steps of the
/// password-change flow inside an encrypted, http-only cookie.
/// </remarks>
public sealed record PendingLoginSession
{
    public required string SessionId { get; init; }
    public required string SessionToken { get; init; }
    public required string UserId { get; init; }
}

/// <summary>Service-internal pairing of the wire result with the secret it must not carry.</summary>
public sealed record LoginOutcome
{
    public required LoginResultDto Result { get; init; }

    /// <summary>Set only when <see cref="LoginStep.PasswordChangeRequired"/> — the step that has a "next call".</summary>
    public PendingLoginSession? Session { get; init; }
}

/// <summary>
/// What the login page knows about the authorization request it was handed.
/// </summary>
public sealed record AuthRequestInfoDto
{
    public required string AuthRequestId { get; init; }

    /// <summary>The address the client suggested, used to prefill the form. Usually null.</summary>
    public string? LoginHint { get; init; }
}
