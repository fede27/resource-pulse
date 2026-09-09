using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace ResourcePulse.Hosting.Auth;

/// <summary>
/// Whether, and whom, to trust when reading <c>X-Forwarded-*</c>.
/// </summary>
/// <remarks>
/// Configuration rather than a code path because there is no answer that is right
/// everywhere: the same image can run behind an ingress, behind a CDN <i>and</i> an
/// ingress, or with nothing in front of it at all, and each of those needs a
/// different trust boundary. Bound from the <c>ForwardedHeaders</c> section.
/// </remarks>
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>Off unless something is actually in front of the application.</summary>
    public bool Enabled { get; set; }

    /// <summary>Exact addresses of the proxies immediately in front (e.g. "10.0.0.7").</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>Networks those proxies live in, CIDR (e.g. "10.42.0.0/16").</summary>
    public string[] KnownNetworks { get; set; } = [];

    /// <summary>
    /// How many hops to walk back through the header. Must match the real chain:
    /// one ingress is 1, a CDN in front of it is 2. Too high and the caller picks
    /// its own address by prepending entries; too low and everyone shares the
    /// proxy's.
    /// </summary>
    public int ForwardLimit { get; set; } = 1;
}

/// <summary>
/// Makes <c>HttpContext.Connection.RemoteIpAddress</c> mean the client rather than
/// the proxy, when a proxy we trust says so (review finding 6).
/// </summary>
/// <remarks>
/// The alternative — reading <c>X-Forwarded-For</c> where the address is consumed,
/// with the socket address as a fallback — cannot work: the header is written by
/// the caller, so anything keyed on it (the login throttle, most obviously) is
/// keyed on a value the attacker chooses, and the fallback branch is never taken
/// because the attacker always sends the header. The trust decision has to be made
/// once, here, from configuration; everything downstream then just reads
/// <c>RemoteIpAddress</c> and is correct in both deployments.
/// </remarks>
public static class ForwardedHeadersSetup
{
    public static void AddResourcePulseForwardedHeaders(this WebApplicationBuilder builder)
    {
        var settings = builder.Configuration
            .GetSection(ForwardedHeadersSettings.SectionName)
            .Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();

        // Registered whether or not it is enabled: the Use side needs to know, and
        // "off" is a decision worth being able to read back.
        builder.Services.AddSingleton(settings);

        if (!settings.Enabled) return;

        // Enabled with nothing trusted is the trap this whole class exists to
        // avoid. ASP.NET Core would silently ignore the header (no known proxy
        // matches the peer), so the deployment would look configured and behave
        // exactly as if it were not. Refuse to start instead.
        if (settings.KnownProxies.Length == 0 && settings.KnownNetworks.Length == 0)
            throw new InvalidOperationException(
                $"{ForwardedHeadersSettings.SectionName}:Enabled is true but neither KnownProxies " +
                "nor KnownNetworks is set. Forwarded headers from an untrusted peer are ignored, " +
                "so this configuration would have no effect. List the proxy addresses or their " +
                "network, or set Enabled to false.");

        if (settings.ForwardLimit < 1)
            throw new InvalidOperationException(
                $"{ForwardedHeadersSettings.SectionName}:ForwardLimit must be at least 1.");

        // Parsed HERE rather than inside the callback below: that one runs when the
        // options are first resolved, so a typo in a CIDR would surface somewhere
        // during pipeline construction instead of while reading configuration.
        var proxies = settings.KnownProxies.Select(ParseAddress).ToList();
        var networks = settings.KnownNetworks.Select(ParseNetwork).ToList();

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = settings.ForwardLimit;

            // The defaults trust loopback, which is not the deployment being
            // described here. Replace the lists wholesale so what is trusted is
            // exactly what configuration says — never clear them WITHOUT adding
            // something, which trusts every caller.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var proxy in proxies)
                options.KnownProxies.Add(proxy);

            foreach (var network in networks)
                options.KnownIPNetworks.Add(network);
        });
    }

    public static void UseResourcePulseForwardedHeaders(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<ForwardedHeadersSettings>();

        if (settings.Enabled)
        {
            app.UseForwardedHeaders();
            return;
        }

        // Not an error — running with nothing in front is a legitimate deployment.
        // But if there IS a proxy and this was forgotten, every caller shares one
        // address and therefore one throttle bucket, which is a denial of service
        // anyone can trigger. Say so once at startup rather than never.
        if (!app.Environment.IsDevelopment())
            app.Logger.LogWarning(
                "Forwarded headers are disabled: the client address is the socket peer. " +
                "If this instance runs behind a proxy, per-caller throttling collapses onto " +
                "the proxy's address — set {Section}:Enabled and its trust list.",
                ForwardedHeadersSettings.SectionName);
    }

    private static IPAddress ParseAddress(string value) =>
        IPAddress.TryParse(value, out var address)
            ? address
            : throw new InvalidOperationException(
                $"{ForwardedHeadersSettings.SectionName}:KnownProxies contains '{value}', " +
                "which is not an IP address.");

    // A host address with a prefix ("10.42.0.5/16") is normalized to its network
    // rather than rejected, which is what whoever typed it meant. A bare address
    // with no prefix is refused: "one host" has to be said in KnownProxies.
    internal static IPNetwork ParseNetwork(string value) =>
        IPNetwork.TryParse(value, out var network)
            ? network
            : throw new InvalidOperationException(
                $"{ForwardedHeadersSettings.SectionName}:KnownNetworks contains '{value}', " +
                "which is not CIDR notation for a network (for example \"10.42.0.0/16\").");
}
