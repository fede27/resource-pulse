using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ResourcePulse.Hosting.Auth;

public static class FakeAuthenticationDefaults
{
    public const string SchemeName = "FakeAuth";
}

public sealed class FakeAuthenticationHandler(
    IOptionsMonitor<FakeAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<FakeAuthenticationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var opts = Options;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, opts.Sub),
            new(ClaimTypes.Email, opts.Email),
            new(ClaimTypes.Name, opts.Name)
        };

        foreach (var (type, value) in opts.Claims)
            claims.Add(new Claim(type, value));

        var identity = new ClaimsIdentity(claims, FakeAuthenticationDefaults.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, FakeAuthenticationDefaults.SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
