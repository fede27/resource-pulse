using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ResourcePulse.Hosting.Auth;

/// <summary>
/// Wires the authentication scheme for the process (ADR-0029).
/// </summary>
/// <remarks>
/// Development supports two providers, selected by <c>Auth:Provider</c>:
/// <list type="bullet">
/// <item><b>Fake</b> (default) — keeps the zero-friction dev loop: no login, no
/// running identity container, seeders and tests unchanged.</item>
/// <item><b>Zitadel</b> — the real OIDC path, opt-in.</item>
/// </list>
/// Production accepts Zitadel only, and refuses to start otherwise.
/// </remarks>
public static class AuthenticationSetup
{
    public const string ZitadelScheme = JwtBearerDefaults.AuthenticationScheme;

    public static void AddResourcePulseAuthentication(
        this WebApplicationBuilder builder)
    {
        var provider = builder.Configuration["Auth:Provider"];
        var useZitadel = string.Equals(provider, "Zitadel", StringComparison.OrdinalIgnoreCase);

        if (!builder.Environment.IsDevelopment())
        {
            if (builder.Configuration.GetSection("FakeAuth").Exists())
                throw new InvalidOperationException(
                    "FakeAuth must not be configured outside Development.");

            if (!useZitadel)
                throw new InvalidOperationException(
                    "Auth:Provider must be 'Zitadel' outside Development.");
        }

        if (useZitadel)
            builder.AddZitadelAuthentication();
        else
            builder.AddFakeAuthentication();
    }

    private static void AddFakeAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddAuthentication(FakeAuthenticationDefaults.SchemeName)
            .AddScheme<FakeAuthenticationOptions, FakeAuthenticationHandler>(
                FakeAuthenticationDefaults.SchemeName,
                opts => builder.Configuration.GetSection("FakeAuth").Bind(opts));
    }

    private static void AddZitadelAuthentication(this WebApplicationBuilder builder)
    {
        var issuer = builder.Configuration["Zitadel:Issuer"]
            ?? throw new InvalidOperationException(
                "Zitadel:Issuer is required when Auth:Provider is 'Zitadel'. In development it " +
                "is generated into zitadel-dev.json by ResourcePulse.DevBootstrap.");

        // Zitadel does not honour a bare `audience` request parameter: the client
        // asks for the project audience through the reserved
        // `urn:zitadel:iam:org:project:id:{projectId}:aud` scope, which lands the
        // project id — and its API applications' client ids — in `aud`. Accepting
        // both keeps validation working whichever the client requested.
        var validAudiences = new[]
            {
                builder.Configuration["Zitadel:ProjectId"],
                builder.Configuration["Zitadel:ApiClientId"]
            }
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a!)
            .ToArray();

        if (validAudiences.Length == 0)
            throw new InvalidOperationException(
                "At least one of Zitadel:ProjectId / Zitadel:ApiClientId is required to validate the audience.");

        builder.Services
            .AddAuthentication(ZitadelScheme)
            .AddJwtBearer(options =>
            {
                // Authority drives OIDC discovery, which supplies the signing keys
                // (JWKS). Validation is therefore entirely offline: no
                // introspection round-trip on the request path.
                options.Authority = issuer;
                options.MetadataAddress = $"{issuer.TrimEnd('/')}/.well-known/openid-configuration";

                // Dev runs Zitadel over plain HTTP on a pinned host so that the
                // issuer string is stable and identical from browser and API.
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudiences = validAudiences,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    // Roles are NOT read from the token: authorization is our
                    // domain (ADR-0029). Pointing the role claim at a name the
                    // IdP never issues makes an accidental [Authorize(Roles=...)]
                    // fail closed instead of silently trusting the IdP.
                    RoleClaimType = "urn:resourcepulse:roles-not-from-idp"
                };
            });
    }
}
