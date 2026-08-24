using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Identity.Login;

/// <summary>
/// Drives Zitadel's Session API on behalf of our own login page (ADR-0031).
/// </summary>
/// <remarks>
/// <para>
/// This is the server side of "bring your own login UI": the browser never talks
/// to Zitadel's session endpoints, because doing so requires a credential with the
/// <c>IAM_LOGIN_CLIENT</c> role that can finalise an authorization request for
/// <b>any</b> user.
/// </para>
/// <para>
/// It sits <b>beside</b> the OIDC flow, not inside it. Everything downstream of
/// the callback URL — code exchange, PKCE, token validation, tenant resolution,
/// membership — is untouched (ADR-0029/0030).
/// </para>
/// </remarks>
public interface IZitadelLoginService
{
    /// <summary>Confirms an authorization request exists and reads its login hint.</summary>
    Task<ServiceResult<AuthRequestInfoDto>> GetAuthRequestAsync(string authRequestId, CancellationToken ct = default);

    /// <summary>Verifies the credentials and, when nothing else is required, finalises the authorization request.</summary>
    Task<ServiceResult<LoginOutcome>> SignInAsync(
        string authRequestId,
        string loginName,
        string password,
        CancellationToken ct = default);

    /// <summary>
    /// Replaces the password of an already password-verified session, then
    /// finalises the authorization request.
    /// </summary>
    Task<ServiceResult<LoginOutcome>> ChangePasswordAsync(
        string authRequestId,
        PendingLoginSession session,
        string newPassword,
        CancellationToken ct = default);
}
