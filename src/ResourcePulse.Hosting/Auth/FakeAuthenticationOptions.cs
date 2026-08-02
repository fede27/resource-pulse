using Microsoft.AspNetCore.Authentication;

namespace ResourcePulse.Hosting.Auth;

public sealed class FakeAuthenticationOptions : AuthenticationSchemeOptions
{
    public string Sub { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The organization the fake principal belongs to. It is resolved through the
    /// real control-plane registry like any other, so the fake path exercises the
    /// same org -> tenant mapping instead of short-circuiting it (ADR-0029).
    /// </summary>
    public string OrganizationId { get; set; } = Seeding.DevDatabaseBootstrapper.FakeOrganizationId;

    public Dictionary<string, string> Claims { get; set; } = [];
}
