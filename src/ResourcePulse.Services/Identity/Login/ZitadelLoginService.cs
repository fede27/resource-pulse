using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ResourcePulse.Common.Results;

namespace ResourcePulse.Services.Identity.Login;

/// <inheritdoc cref="IZitadelLoginService"/>
public sealed class ZitadelLoginService(HttpClient http, ILogger<ZitadelLoginService> logger)
    : IZitadelLoginService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The one message every credential failure collapses into.
    /// </summary>
    /// <remarks>
    /// Zitadel distinguishes "no such user" from "wrong password", and the
    /// distinction is exactly what turns an anonymous endpoint into an account
    /// enumeration oracle. The page shows a single sentence for both; the real
    /// reason is logged server-side.
    /// </remarks>
    private const string InvalidCredentialsMessage = "Invalid credentials.";

    /// <summary>Zitadel error ids that mean "this account is locked out", not "wrong password".</summary>
    /// <remarks>
    /// Deliberately <b>not</b> collapsed into <see cref="InvalidCredentialsMessage"/>.
    /// It does confirm the account exists, but the alternative is worse: somebody
    /// who is locked out reads "wrong password", keeps trying, and stays locked.
    /// Every mainstream identity provider makes the same trade.
    /// </remarks>
    private static readonly string[] LockoutErrorIds = ["COMMAND-JLK35", "COMMAND-SFA3t"];

    public async Task<ServiceResult<AuthRequestInfoDto>> GetAuthRequestAsync(
        string authRequestId,
        CancellationToken ct = default)
    {
        var (ok, payload) = await SendAsync(
            HttpMethod.Get, $"/v2/oidc/auth_requests/{Uri.EscapeDataString(authRequestId)}", null, ct);

        if (!ok)
            return ServiceResult<AuthRequestInfoDto>.NotFound(
                "The authorization request is unknown or has expired.");

        return ServiceResult<AuthRequestInfoDto>.Success(new AuthRequestInfoDto
        {
            AuthRequestId = authRequestId,
            LoginHint = payload?["authRequest"]?["loginHint"]?.GetValue<string>()
        });
    }

    public async Task<ServiceResult<LoginOutcome>> SignInAsync(
        string authRequestId,
        string loginName,
        string password,
        CancellationToken ct = default)
    {
        var authRequest = await GetAuthRequestAsync(authRequestId, ct);
        if (authRequest.IsFailure) return ServiceResult<LoginOutcome>.Failure(authRequest.Error!);

        // The user check and the password check go in ONE call on purpose: Zitadel
        // resolves the login name and verifies the password atomically, so this
        // service never has a branch that distinguishes "unknown user" from "wrong
        // password" — the enumeration oracle cannot be reintroduced by accident.
        var (created, session) = await SendAsync(HttpMethod.Post, "/v2/sessions", new
        {
            checks = new
            {
                user = new { loginName },
                password = new { password }
            }
        }, ct);

        if (!created) return CredentialFailure(loginName, session);

        var pending = new PendingLoginSession
        {
            SessionId = session!["sessionId"]!.GetValue<string>(),
            SessionToken = session["sessionToken"]!.GetValue<string>(),
            UserId = string.Empty
        };

        var userId = await ReadVerifiedUserIdAsync(pending.SessionId, ct);
        if (userId is null)
            return ServiceResult<LoginOutcome>.Failure(
                ServiceError.Failure("The session was created but carries no verified user."));

        pending = pending with { UserId = userId };

        // Fail SAFE, not fail open. Zitadel does not re-check the login policy when
        // an authorization request is finalised — the login UI is trusted to have
        // done it. So a user carrying a second factor must NOT be completed here on
        // a password alone: that would silently downgrade their authentication.
        if (await HasFactorBeyondPasswordAsync(userId, ct))
            return ServiceResult<LoginOutcome>.Success(new LoginOutcome
            {
                Result = new LoginResultDto { Step = LoginStep.AdditionalFactorRequired }
            });

        if (await IsPasswordChangeRequiredAsync(userId, ct))
            return ServiceResult<LoginOutcome>.Success(new LoginOutcome
            {
                Result = new LoginResultDto { Step = LoginStep.PasswordChangeRequired },
                Session = pending
            });

        return await FinaliseAsync(authRequestId, pending, ct);
    }

    public async Task<ServiceResult<LoginOutcome>> ChangePasswordAsync(
        string authRequestId,
        PendingLoginSession session,
        string newPassword,
        CancellationToken ct = default)
    {
        // No `verification` block: the login client holds `user.credential.write`,
        // and the right to set this password was established by the password check
        // that produced the session carried in the (encrypted, http-only) cookie.
        // Re-collecting the current password would mean either asking for it twice
        // or stashing it somewhere — both worse than relying on the verified
        // session we already hold.
        var (ok, payload) = await SendAsync(
            HttpMethod.Post,
            $"/v2/users/{Uri.EscapeDataString(session.UserId)}/password",
            new { newPassword = new { password = newPassword, changeRequired = false } },
            ct);

        if (!ok)
        {
            logger.LogWarning("Zitadel refused the password change for user {UserId}: {Payload}",
                session.UserId, payload?.ToJsonString());

            // Zitadel enforces the instance's password complexity policy here, so
            // this is nearly always a rejected password rather than a broken call.
            return ServiceResult<LoginOutcome>.Validation(new Dictionary<string, string[]>
            {
                ["newPassword"] = [ExtractMessage(payload) ?? "The new password does not meet the password policy."]
            });
        }

        return await FinaliseAsync(authRequestId, session, ct);
    }

    /// <summary>Exchanges a verified session for the callback URL that carries the authorization code.</summary>
    private async Task<ServiceResult<LoginOutcome>> FinaliseAsync(
        string authRequestId,
        PendingLoginSession session,
        CancellationToken ct)
    {
        var (ok, payload) = await SendAsync(
            HttpMethod.Post,
            $"/v2/oidc/auth_requests/{Uri.EscapeDataString(authRequestId)}",
            new { session = new { sessionId = session.SessionId, sessionToken = session.SessionToken } },
            ct);

        if (!ok)
        {
            logger.LogWarning("Zitadel refused to finalise auth request {AuthRequestId}: {Payload}",
                authRequestId, payload?.ToJsonString());

            return ServiceResult<LoginOutcome>.Conflict(
                "The sign-in could not be completed. Start again from the application.");
        }

        var callbackUrl = payload?["callbackUrl"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(callbackUrl))
            return ServiceResult<LoginOutcome>.Failure(
                ServiceError.Failure("Zitadel returned no callback URL."));

        return ServiceResult<LoginOutcome>.Success(new LoginOutcome
        {
            Result = new LoginResultDto { Step = LoginStep.Completed, CallbackUrl = callbackUrl }
        });
    }

    private async Task<string?> ReadVerifiedUserIdAsync(string sessionId, CancellationToken ct)
    {
        var (ok, payload) = await SendAsync(
            HttpMethod.Get, $"/v2/sessions/{Uri.EscapeDataString(sessionId)}", null, ct);

        return ok ? payload?["session"]?["factors"]?["user"]?["id"]?.GetValue<string>() : null;
    }

    private async Task<bool> IsPasswordChangeRequiredAsync(string userId, CancellationToken ct)
    {
        var (ok, payload) = await SendAsync(
            HttpMethod.Get, $"/v2/users/{Uri.EscapeDataString(userId)}", null, ct);

        return ok && payload?["user"]?["human"]?["passwordChangeRequired"]?.GetValue<bool>() == true;
    }

    private async Task<bool> HasFactorBeyondPasswordAsync(string userId, CancellationToken ct)
    {
        var (ok, payload) = await SendAsync(
            HttpMethod.Get, $"/v2/users/{Uri.EscapeDataString(userId)}/authentication_methods", null, ct);

        // Unreadable factors are treated as "there might be one": the safe answer
        // to "can this password alone authenticate them?" is no.
        if (!ok) return true;

        if (payload?["authMethodTypes"] is not JsonArray methods) return false;

        return methods.Any(m =>
            m?.GetValue<string>() is { } type
            && type is not ("AUTHENTICATION_METHOD_TYPE_PASSWORD" or "AUTHENTICATION_METHOD_TYPE_UNSPECIFIED"));
    }

    private ServiceResult<LoginOutcome> CredentialFailure(string loginName, JsonNode? payload)
    {
        var raw = payload?.ToJsonString() ?? "<empty>";
        logger.LogInformation("Failed sign-in for {LoginName}: {Payload}", loginName, raw);

        if (LockoutErrorIds.Any(id => raw.Contains(id, StringComparison.Ordinal)))
            return ServiceResult<LoginOutcome>.Validation(new Dictionary<string, string[]>
            {
                ["credentials"] = ["This account is temporarily locked after too many failed attempts."]
            });

        return ServiceResult<LoginOutcome>.Validation(new Dictionary<string, string[]>
        {
            ["credentials"] = [InvalidCredentialsMessage]
        });
    }

    private static string? ExtractMessage(JsonNode? payload) =>
        payload?["message"]?.GetValue<string>();

    /// <summary>
    /// One call against Zitadel. Never throws on an HTTP status: every caller here
    /// has to translate a refusal into a <see cref="ServiceResult{T}"/> anyway, and
    /// exceptions would route a routine wrong password through the global handler
    /// as a 500.
    /// </summary>
    private async Task<(bool Ok, JsonNode? Payload)> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e, "Zitadel {Method} {Path} could not be reached", method, path);
            return (false, null);
        }

        using (response)
        {
            var raw = await response.Content.ReadAsStringAsync(ct);
            JsonNode? payload = null;

            if (!string.IsNullOrWhiteSpace(raw))
            {
                try { payload = JsonNode.Parse(raw); }
                catch (JsonException) { /* a non-JSON body is a refusal we cannot read further */ }
            }

            if (!response.IsSuccessStatusCode && response.StatusCode == HttpStatusCode.Unauthorized)
                logger.LogError(
                    "Zitadel rejected the login client's token on {Method} {Path}. The PAT in " +
                    "Zitadel:LoginClientToken is missing, expired, or lacks IAM_LOGIN_CLIENT.",
                    method, path);

            return (response.IsSuccessStatusCode, payload);
        }
    }
}
