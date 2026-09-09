using System.Net.Http.Headers;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using ResourcePulse.Http.Auth;
using ResourcePulse.Services.Identity.Login;

namespace ResourcePulse.Hosting.Auth;

/// <summary>
/// How hard the anonymous sign-in endpoints are throttled.
/// </summary>
/// <remarks>
/// Configurable so the limit can be loosened without a code change if it ever
/// gets in a real user's way. Deliberately NOT switchable off: this is the only
/// unauthenticated surface in the application, and "no throttle at all" should
/// cost more than editing one key.
/// </remarks>
public sealed class LoginRateLimitSettings
{
    public const string SectionName = "Login:RateLimit";

    /// <summary>
    /// Roomy enough that a person mistyping their password never notices, tight
    /// enough that scripted spraying does.
    /// </summary>
    public int PermitLimit { get; set; } = 20;

    public int WindowSeconds { get; set; } = 60;
}

/// <summary>
/// Wires the server side of our own login page (ADR-0031): the Zitadel client that
/// holds the login-client credential, and the throttle in front of it.
/// </summary>
public static class LoginClientSetup
{
    public static void AddResourcePulseLoginClient(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<PendingLoginSessionCookie>();

        var rateLimit = builder.Configuration
            .GetSection(LoginRateLimitSettings.SectionName)
            .Get<LoginRateLimitSettings>() ?? new LoginRateLimitSettings();

        if (rateLimit.PermitLimit < 1 || rateLimit.WindowSeconds < 1)
            throw new InvalidOperationException(
                $"{LoginRateLimitSettings.SectionName}: PermitLimit and WindowSeconds must both be " +
                "at least 1. Raise the limit to relax the throttle; it cannot be removed.");

        builder.Services.AddRateLimiter(options =>
        {
            // The default rejection status is 503, which tells a client the server
            // is broken and invites a retry. 429 says what actually happened.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(
                LoginRateLimiting.PolicyName,
                // Partitioned by remote address: the thing being limited is a
                // caller, not an account. Zitadel's own lockout already protects
                // one account against many guesses; what it cannot see is one
                // caller trying one password against a thousand accounts.
                //
                // RemoteIpAddress, never a header — behind a proxy it is
                // ForwardedHeadersSetup's job to have made that the client address
                // already, under a trust list nobody but us controls.
                context => RateLimitPartition.GetFixedWindowLimiter(
                    LoginRateLimiting.PartitionKeyFor(context.Connection.RemoteIpAddress),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimit.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimit.WindowSeconds),
                        QueueLimit = 0
                    }));
        });

        var issuer = builder.Configuration["Zitadel:Issuer"];
        var token = builder.Configuration["Zitadel:LoginClientToken"];

        // Absent configuration is the NORMAL development case: FakeAuth is the
        // default provider (ADR-0029), no identity container is running, and there
        // is no login page to serve. The client is registered anyway so the
        // controller resolves; its calls simply fail, and the page is unreachable
        // because the SPA redirects away from /login when OIDC is not configured.
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(token))
        {
            if (!builder.Environment.IsDevelopment())
                throw new InvalidOperationException(
                    "Zitadel:Issuer and Zitadel:LoginClientToken are required outside Development: " +
                    "without them the sign-in page cannot complete an authorization request.");

            builder.Services.AddHttpClient<IZitadelLoginService, ZitadelLoginService>();
            return;
        }

        builder.Services.AddHttpClient<IZitadelLoginService, ZitadelLoginService>(client =>
        {
            client.BaseAddress = new Uri(issuer);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.Timeout = TimeSpan.FromSeconds(15);
        });
    }
}
