using Microsoft.AspNetCore.Authentication;

namespace ResourcePulse.Hosting.Auth;

public sealed class FakeAuthenticationOptions : AuthenticationSchemeOptions
{
    public string Sub { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Claims { get; set; } = [];
}
