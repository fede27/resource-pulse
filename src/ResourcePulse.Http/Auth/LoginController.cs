using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ResourcePulse.Common.Results;
using ResourcePulse.Services.Identity.Login;

namespace ResourcePulse.Http.Auth;

/// <summary>
/// The server side of our own login page (ADR-0031).
/// </summary>
/// <remarks>
/// <para>
/// <b>The only anonymous surface in the application</b>, and it exists for one
/// reason: driving Zitadel's Session API requires a credential with the
/// <c>IAM_LOGIN_CLIENT</c> role, which can finalise an authorization request for
/// any user. That credential cannot live in a browser, so these three endpoints
/// stand between the page and Zitadel.
/// </para>
/// <para>
/// Being anonymous, it is exempt from the <c>Viewer</c> fallback policy, from
/// tenant resolution and from access resolution — necessarily so: the caller has
/// no token yet, therefore no organization, therefore no tenant. It is also the
/// deliberate exception recorded in <c>EndpointAuthorizationTests</c>: every other
/// write endpoint in this assembly must name a role.
/// </para>
/// <para>
/// Nothing here decides who may do what. Authorization still happens exactly where
/// it did before — the membership store, after the token comes back (ADR-0030).
/// </para>
/// </remarks>
[Route("api/auth")]
[ApiController]
[AllowAnonymous]
[EnableRateLimiting(LoginRateLimiting.PolicyName)]
public sealed class LoginController(
    IZitadelLoginService service,
    PendingLoginSessionCookie sessionCookie) : ControllerFoundation
{
    /// <summary>
    /// Confirms the authorization request the page was handed, and returns the
    /// login hint to prefill with.
    /// </summary>
    [HttpGet("auth-request/{authRequestId}")]
    [ProducesResponseType<AuthRequestInfoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuthRequestAsync(string authRequestId, CancellationToken ct) =>
        FromResult(await service.GetAuthRequestAsync(authRequestId, ct));

    /// <summary>Verifies credentials and, when nothing more is required, completes the sign-in.</summary>
    [HttpPost("login")]
    [ProducesResponseType<LoginResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequestDto dto, CancellationToken ct)
    {
        var result = await service.SignInAsync(dto.AuthRequestId, dto.LoginName, dto.Password, ct);
        return Respond(result);
    }

    /// <summary>Replaces a password Zitadel demanded be changed, then completes the sign-in.</summary>
    /// <remarks>
    /// The session proving the previous password was correct comes from the
    /// http-only cookie, never from the request body — otherwise anybody could set
    /// anybody's password by naming their user id.
    /// </remarks>
    [HttpPost("password-change")]
    [ProducesResponseType<LoginResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangePasswordAsync(
        [FromBody] ChangePasswordRequestDto dto,
        CancellationToken ct)
    {
        var pending = sessionCookie.Read(Request);

        // `Problem(...)` already IS the result. Wrapping it in `Conflict(...)`
        // serializes the ObjectResult itself — `{"value":{…},"formatters":[],…}` —
        // and the client, which reads ProblemDetails, finds no `detail` to show.
        if (pending is null)
            return Problem(
                "The sign-in has expired. Start again.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict");

        var result = await service.ChangePasswordAsync(dto.AuthRequestId, pending, dto.NewPassword, ct);
        return Respond(result);
    }

    /// <summary>
    /// Splits the outcome: the public half goes on the wire, the session half goes
    /// into the cookie. This is the one place that knows the difference.
    /// </summary>
    private IActionResult Respond(ServiceResult<LoginOutcome> result)
    {
        if (result.IsFailure) return FromResult(result);

        var outcome = result.Value;

        if (outcome.Session is not null) sessionCookie.Write(Response, outcome.Session);
        // Nothing further to authorise once the callback URL is out: the cookie
        // would only be a credential left lying around.
        else sessionCookie.Clear(Response);

        return Ok(outcome.Result);
    }
}
