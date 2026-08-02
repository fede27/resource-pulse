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
    /// client secret (a SPA cannot hold one), JWT access tokens.
    /// </summary>
    public Task<string> EnsureSpaApplicationAsync(
        string projectId,
        string name,
        IReadOnlyList<string> redirectUris,
        IReadOnlyList<string> postLogoutUris,
        CancellationToken ct) =>
        EnsureApplicationAsync(projectId, name, "oidc", new
        {
            name,
            redirectUris,
            postLogoutRedirectUris = postLogoutUris,
            responseTypes = new[] { "OIDC_RESPONSE_TYPE_CODE" },
            grantTypes = new[] { "OIDC_GRANT_TYPE_AUTHORIZATION_CODE", "OIDC_GRANT_TYPE_REFRESH_TOKEN" },
            appType = "OIDC_APP_TYPE_USER_AGENT",
            // NONE = public client. PKCE is what secures the exchange.
            authMethodType = "OIDC_AUTH_METHOD_TYPE_NONE",
            // JWT access tokens: the API validates them offline against the
            // JWKS from the discovery document, with no introspection round-trip.
            accessTokenType = "OIDC_TOKEN_TYPE_JWT",
            // Roles deliberately stay OUT of the token: authorization is our
            // domain, the IdP is authoritative on identity only (ADR-0029).
            accessTokenRoleAssertion = false,
            idTokenRoleAssertion = false,
            idTokenUserinfoAssertion = false,
            // devMode relaxes the redirect-URI checks so plain http://localhost
            // is accepted. Dev only.
            devMode = true
        }, ct);

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
        var search = await SendAsync(
            Request(HttpMethod.Post, $"/management/v1/projects/{projectId}/apps/_search", new
            {
                queries = new object[] { new { nameQuery = new { name, method = "TEXT_QUERY_METHOD_EQUALS" } } }
            }), ct);

        if (search["result"] is JsonArray existing && existing.Count > 0)
        {
            var app = existing[0]!;
            var clientId =
                app["oidcConfig"]?["clientId"]?.GetValue<string>()
                ?? app["apiConfig"]?["clientId"]?.GetValue<string>();

            if (clientId is not null)
                return clientId;
        }

        var created = await SendAsync(
            Request(HttpMethod.Post, $"/management/v1/projects/{projectId}/apps/{kind}", body), ct);
        return created["clientId"]!.GetValue<string>();
    }
}
