using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ResourcePulse.DevBootstrap;

/// <summary>
/// A thin, idempotent wrapper over the slice of the Zitadel Management API the
/// dev bootstrap needs.
/// </summary>
/// <remarks>
/// Every <c>Ensure*</c> method searches before it creates. Re-running the
/// bootstrap N times must converge on the same objects rather than accumulate
/// duplicates — Zitadel happily accepts two projects with the same name, so
/// idempotence has to be enforced here, not hoped for.
/// </remarks>
public sealed class ZitadelManagementClient(HttpClient http, string personalAccessToken)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private HttpRequestMessage Request(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", personalAccessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);
        return request;
    }

    private async Task<JsonNode> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await http.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Zitadel {request.Method} {request.RequestUri} failed with {(int)response.StatusCode}: {payload}");

        return string.IsNullOrWhiteSpace(payload)
            ? JsonNode.Parse("{}")!
            : JsonNode.Parse(payload)!;
    }

    /// <summary>
    /// Like <see cref="SendAsync"/>, but treats one specific Zitadel error id as
    /// success.
    /// </summary>
    /// <remarks>
    /// For the handful of calls whose failure mode <i>is</i> the desired state.
    /// Matching on the error id rather than the status code keeps it narrow: a
    /// genuine 400 still throws.
    /// </remarks>
    private async Task SendTolerantAsync(HttpRequestMessage request, string tolerated, CancellationToken ct)
    {
        using var response = await http.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode || payload.Contains(tolerated, StringComparison.Ordinal)) return;

        throw new InvalidOperationException(
            $"Zitadel {request.Method} {request.RequestUri} failed with {(int)response.StatusCode}: {payload}");
    }

    /// <summary>The organization the machine user belongs to — the lookup key our control plane maps.</summary>
    public async Task<(string Id, string Name)> GetOrganizationAsync(CancellationToken ct)
    {
        var result = await SendAsync(Request(HttpMethod.Get, "/management/v1/orgs/me"), ct);
        var org = result["org"]!;
        return (org["id"]!.GetValue<string>(), org["name"]?.GetValue<string>() ?? "default");
    }

    public async Task<string> EnsureProjectAsync(string name, CancellationToken ct)
    {
        var search = await SendAsync(
            Request(HttpMethod.Post, "/management/v1/projects/_search", new
            {
                queries = new object[] { new { nameQuery = new { name, method = "TEXT_QUERY_METHOD_EQUALS" } } }
            }), ct);

        if (search["result"] is JsonArray existing && existing.Count > 0)
            return existing[0]!["id"]!.GetValue<string>();

        var created = await SendAsync(
            Request(HttpMethod.Post, "/management/v1/projects", new { name }), ct);
        return created["id"]!.GetValue<string>();
    }

    /// <summary>
    /// Creates the browser-facing application: Authorization Code + PKCE, no
    /// client secret (a SPA cannot hold one), JWT access tokens — and points it
    /// at OUR login page (ADR-0031).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="loginBaseUri"/> lands in the application's
    /// <c>loginVersion</c>. It is set <b>per application</b> rather than through
    /// the instance-wide <c>LoginV2</c> feature flag on purpose: the flag applies
    /// to every application in the instance, which would send the Zitadel Console
    /// through our login page too — and our page can only finalise auth requests
    /// for our own project, so the administrator would be locked out of the
    /// console.
    /// </para>
    /// <para>
    /// It is a <b>base</b>, not the page's URL: Zitadel appends its own route, so
    /// an authorization request arrives at <c>{base}/login?authRequest=…</c>.
    /// Passing the page's own address yields <c>/login/login</c>.
    /// </para>
    /// <para>
    /// Unlike the other <c>Ensure*</c> methods this one does not stop at "already
    /// exists": it re-applies the configuration every run, so a changed redirect
    /// URI or login URI converges instead of silently keeping the first value
    /// ever written. <c>UpdateOIDCAppConfig</c> is a <b>full replace</b>, which is
    /// why the body is built once and used for both paths.
    /// </para>
    /// </remarks>
    public async Task<string> EnsureSpaApplicationAsync(
        string projectId,
        string name,
        IReadOnlyList<string> redirectUris,
        IReadOnlyList<string> postLogoutUris,
        string loginBaseUri,
        CancellationToken ct)
    {
        var config = new Dictionary<string, object>
        {
            ["redirectUris"] = redirectUris,
            ["postLogoutRedirectUris"] = postLogoutUris,
            ["responseTypes"] = new[] { "OIDC_RESPONSE_TYPE_CODE" },
            ["grantTypes"] = new[] { "OIDC_GRANT_TYPE_AUTHORIZATION_CODE", "OIDC_GRANT_TYPE_REFRESH_TOKEN" },
            ["appType"] = "OIDC_APP_TYPE_USER_AGENT",
            // NONE = public client. PKCE is what secures the exchange.
            ["authMethodType"] = "OIDC_AUTH_METHOD_TYPE_NONE",
            // JWT access tokens: the API validates them offline against the
            // JWKS from the discovery document, with no introspection round-trip.
            ["accessTokenType"] = "OIDC_TOKEN_TYPE_JWT",
            // Roles deliberately stay OUT of the token: authorization is our
            // domain, the IdP is authoritative on identity only (ADR-0029).
            ["accessTokenRoleAssertion"] = false,
            ["idTokenRoleAssertion"] = false,
            ["idTokenUserinfoAssertion"] = false,
            // devMode relaxes the redirect-URI checks so plain http://localhost
            // is accepted. Dev only.
            ["devMode"] = true,
            // Auth requests for THIS application are redirected here instead of
            // to Zitadel's hosted login (ADR-0031).
            ["loginVersion"] = new { loginV2 = new { baseUri = loginBaseUri } }
        };

        var existing = await FindApplicationAsync(projectId, name, ct);

        if (existing is null)
        {
            var body = new Dictionary<string, object>(config) { ["name"] = name };
            var created = await SendAsync(
                Request(HttpMethod.Post, $"/management/v1/projects/{projectId}/apps/oidc", body), ct);
            return created["clientId"]!.GetValue<string>();
        }

        // Zitadel REJECTS a no-op update: re-sending an identical configuration
        // comes back 400 "No changes (COMMAND-1m88i)". Since converging is the
        // whole point of running this every time, the steady state — nothing to
        // change — is a success, not a failure. Left unhandled it aborted the
        // bootstrap on the second run, and `WaitForCompletion` then kept the API
        // and the SPA from ever starting.
        await SendTolerantAsync(
            Request(HttpMethod.Put,
                $"/management/v1/projects/{projectId}/apps/{existing.Value.AppId}/oidc_config",
                config),
            tolerated: "COMMAND-1m88i",
            ct);

        return existing.Value.ClientId;
    }

    /// <summary>
    /// Ensures the machine user whose token lets the API drive the Session API on
    /// behalf of a user who is signing in through our own login page (ADR-0031).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>IAM_LOGIN_CLIENT</c> role is what
    /// <c>POST /v2/oidc/auth_requests/{id}</c> demands. This token can complete an
    /// authorization for <i>any</i> user, so it never reaches the browser: only
    /// the API holds it.
    /// </para>
    /// <para>
    /// The idempotence rule here is different from every other <c>Ensure*</c>
    /// method, and the difference matters: a personal access token's <b>value is
    /// returned once, at creation, and can never be read again</b>. "The machine
    /// user exists" therefore does not imply "we have a usable token" — the file
    /// holding it may have been deleted, or the token may have expired. So the
    /// existing token is <i>probed</i>, and a new one is minted only when the
    /// probe fails.
    /// </para>
    /// </remarks>
    /// <param name="existingToken">The token from a previous run, if any.</param>
    /// <returns>A working personal access token for the login client.</returns>
    public async Task<string> EnsureLoginClientAsync(
        string username,
        string? existingToken,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(existingToken) && await IsTokenUsableAsync(existingToken, ct))
            return existingToken;

        var userId = await FindMachineUserAsync(username, ct);

        if (userId is null)
        {
            var created = await SendAsync(
                Request(HttpMethod.Post, "/management/v1/users/machine", new
                {
                    userName = username,
                    name = "Login Client",
                    description = "Drives the Session API for the custom login page (ADR-0031)."
                }), ct);

            userId = created["userId"]!.GetValue<string>();
        }

        await EnsureIamMemberAsync(userId, "IAM_LOGIN_CLIENT", ct);

        var pat = await SendAsync(
            Request(HttpMethod.Post, $"/management/v1/users/{userId}/pats", new
            {
                expirationDate = "2030-01-01T00:00:00Z"
            }), ct);

        return pat["token"]!.GetValue<string>();
    }

    /// <summary>Probes a token against the API a machine user can always call on itself.</summary>
    private async Task<bool> IsTokenUsableAsync(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/v1/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    private async Task<string?> FindMachineUserAsync(string username, CancellationToken ct)
    {
        var search = await SendAsync(
            Request(HttpMethod.Post, "/management/v1/users/_search", new
            {
                queries = new object[]
                {
                    new { userNameQuery = new { userName = username, method = "TEXT_QUERY_METHOD_EQUALS" } }
                }
            }), ct);

        return search["result"] is JsonArray found && found.Count > 0
            ? found[0]!["id"]!.GetValue<string>()
            : null;
    }

    private async Task EnsureIamMemberAsync(string userId, string role, CancellationToken ct)
    {
        var members = await SendAsync(
            Request(HttpMethod.Post, "/admin/v1/members/_search", new
            {
                queries = new object[] { new { userIdQuery = new { userId } } }
            }), ct);

        if (members["result"] is JsonArray existing && existing.Count > 0)
        {
            var roles = existing[0]!["roles"] as JsonArray;
            if (roles?.Any(r => r?.GetValue<string>() == role) == true) return;
        }

        await SendAsync(
            Request(HttpMethod.Post, "/admin/v1/members", new { userId, roles = new[] { role } }), ct);
    }

    /// <summary>
    /// Points the instance at the development mail sink, and activates it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Zitadel sends mail on registration; with no SMTP provider it fails with
    /// <c>Errors.SMTPConfig.NotFound</c> and the user is left uninitialised.
    /// </para>
    /// <para>
    /// This is done through the admin API rather than
    /// <c>ZITADEL_DEFAULTINSTANCE_SMTPCONFIGURATION_*</c> on purpose: those
    /// environment variables are only read when the instance is FIRST created,
    /// so on an already-provisioned instance (the normal case after the first
    /// run) they do nothing. The API call converges either way.
    /// </para>
    /// <para>
    /// A newly created provider is <c>SMTP_CONFIG_INACTIVE</c>; creating without
    /// activating leaves mail just as broken, only more confusingly.
    /// </para>
    /// </remarks>
    public async Task<string> EnsureSmtpProviderAsync(
        string description,
        string host,
        string senderAddress,
        string senderName,
        CancellationToken ct)
    {
        var existing = await SendAsync(
            Request(HttpMethod.Post, "/admin/v1/smtp/_search", new { }), ct);

        if (existing["result"] is JsonArray providers)
        {
            foreach (var provider in providers)
            {
                if (provider?["description"]?.GetValue<string>() != description) continue;

                var id = provider["id"]!.GetValue<string>();

                // Re-activate rather than assume: a provider can exist but have
                // been deactivated by hand in the console.
                if (provider["state"]?.GetValue<string>() != "SMTP_CONFIG_ACTIVE")
                    await SendAsync(Request(HttpMethod.Post, $"/admin/v1/smtp/{id}/_activate"), ct);

                return id;
            }
        }

        var created = await SendAsync(
            Request(HttpMethod.Post, "/admin/v1/smtp", new
            {
                description,
                host,
                senderAddress,
                senderName,
                // The sink speaks plain SMTP on the container network; there is
                // nothing to encrypt and no certificate to trust.
                tls = false,
                user = string.Empty
            }), ct);

        var createdId = created["id"]!.GetValue<string>();
        await SendAsync(Request(HttpMethod.Post, $"/admin/v1/smtp/{createdId}/_activate"), ct);
        return createdId;
    }

    public Task<string> EnsureApiApplicationAsync(string projectId, string name, CancellationToken ct) =>
        EnsureApplicationAsync(projectId, name, "api", new
        {
            name,
            authMethodType = "API_AUTH_METHOD_TYPE_PRIVATE_KEY_JWT"
        }, ct);

    private async Task<string> EnsureApplicationAsync(
        string projectId,
        string name,
        string kind,
        object body,
        CancellationToken ct)
    {
        var existing = await FindApplicationAsync(projectId, name, ct);
        if (existing is not null) return existing.Value.ClientId;

        var created = await SendAsync(
            Request(HttpMethod.Post, $"/management/v1/projects/{projectId}/apps/{kind}", body), ct);
        return created["clientId"]!.GetValue<string>();
    }

    /// <summary>
    /// The application's id and client id, or <c>null</c> when it does not exist.
    /// </summary>
    /// <remarks>
    /// The <b>app id</b> is what addresses the configuration endpoints; the
    /// <b>client id</b> is what OIDC clients authenticate with. They are different
    /// identifiers and Zitadel needs the first one to update an app in place.
    /// </remarks>
    private async Task<(string AppId, string ClientId)?> FindApplicationAsync(
        string projectId,
        string name,
        CancellationToken ct)
    {
        var search = await SendAsync(
            Request(HttpMethod.Post, $"/management/v1/projects/{projectId}/apps/_search", new
            {
                queries = new object[] { new { nameQuery = new { name, method = "TEXT_QUERY_METHOD_EQUALS" } } }
            }), ct);

        if (search["result"] is not JsonArray found || found.Count == 0) return null;

        var app = found[0]!;
        var clientId =
            app["oidcConfig"]?["clientId"]?.GetValue<string>()
            ?? app["apiConfig"]?["clientId"]?.GetValue<string>();

        return clientId is null ? null : (app["id"]!.GetValue<string>(), clientId);
    }
}
