using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ResourcePulse.Hosting.Auth;
using IPNetwork = System.Net.IPNetwork;

namespace ResourcePulse.Application.Tests;

// Trusting X-Forwarded-* is a decision made once, from configuration (review
// finding 6). These tests pin the two ways it can be got wrong: trusting nobody
// while believing you enabled it, and trusting everybody.
public class ForwardedHeadersSetupTests
{
    private static WebApplicationBuilder BuilderWith(params (string Key, string? Value)[] settings)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Configuration.AddInMemoryCollection(
            settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)));
        return builder;
    }

    [Fact]
    public void Disabled_IsTheDefaultAndBindsCleanly()
    {
        var builder = BuilderWith();

        builder.AddResourcePulseForwardedHeaders();

        using var provider = builder.Services.BuildServiceProvider();
        provider.GetRequiredService<ForwardedHeadersSettings>().Enabled.Should().BeFalse();
    }

    [Fact]
    public void EnabledWithAnEmptyTrustList_RefusesToStart()
    {
        // The trap: ASP.NET Core ignores the header when no known proxy matches the
        // peer, so this would look configured and behave exactly as if it were not.
        var builder = BuilderWith(("ForwardedHeaders:Enabled", "true"));

        var act = () => builder.AddResourcePulseForwardedHeaders();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*neither KnownProxies nor KnownNetworks*");
    }

    [Fact]
    public void EnabledWithAProxy_ReplacesTheLoopbackDefaults()
    {
        var builder = BuilderWith(
            ("ForwardedHeaders:Enabled", "true"),
            ("ForwardedHeaders:KnownProxies:0", "10.0.0.7"),
            ("ForwardedHeaders:KnownNetworks:0", "10.42.0.0/16"),
            ("ForwardedHeaders:ForwardLimit", "2"));

        builder.AddResourcePulseForwardedHeaders();

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        options.ForwardedHeaders.Should().HaveFlag(ForwardedHeaders.XForwardedFor);
        options.ForwardLimit.Should().Be(2);
        // Exactly what configuration named — the defaults trust loopback, and
        // whatever is in front of a real deployment is not that.
        options.KnownProxies.Should().ContainSingle().Which.Should().Be(IPAddress.Parse("10.0.0.7"));
        options.KnownIPNetworks.Should().ContainSingle()
            .Which.Should().Be(IPNetwork.Parse("10.42.0.0/16"));
    }

    [Fact]
    public void AMalformedNetwork_FailsWhileReadingConfiguration()
    {
        // Not later, when the pipeline first resolves the options.
        var builder = BuilderWith(
            ("ForwardedHeaders:Enabled", "true"),
            ("ForwardedHeaders:KnownNetworks:0", "10.42.0.0"));

        var act = () => builder.AddResourcePulseForwardedHeaders();

        act.Should().Throw<InvalidOperationException>().WithMessage("*CIDR*");
    }

    [Fact]
    public void AHostAddressWithAPrefix_IsNormalizedToItsNetwork()
    {
        // "10.42.0.5/16" means the /16, which is what whoever typed it meant. Worth
        // pinning: the trust list is the security boundary, so how it reads an
        // imprecise entry should not be a surprise discovered in production.
        var builder = BuilderWith(
            ("ForwardedHeaders:Enabled", "true"),
            ("ForwardedHeaders:KnownNetworks:0", "10.42.0.5/16"));

        builder.AddResourcePulseForwardedHeaders();

        using var provider = builder.Services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value
            .KnownIPNetworks.Should().ContainSingle()
            .Which.Should().Be(IPNetwork.Parse("10.42.0.0/16"));
    }

    [Fact]
    public void AMalformedProxyAddress_FailsWhileReadingConfiguration()
    {
        var builder = BuilderWith(
            ("ForwardedHeaders:Enabled", "true"),
            ("ForwardedHeaders:KnownProxies:0", "not-an-address"));

        var act = () => builder.AddResourcePulseForwardedHeaders();

        act.Should().Throw<InvalidOperationException>().WithMessage("*not an IP address*");
    }

    [Fact]
    public void ZeroHops_IsRejected()
    {
        var builder = BuilderWith(
            ("ForwardedHeaders:Enabled", "true"),
            ("ForwardedHeaders:KnownProxies:0", "10.0.0.7"),
            ("ForwardedHeaders:ForwardLimit", "0"));

        var act = () => builder.AddResourcePulseForwardedHeaders();

        act.Should().Throw<InvalidOperationException>().WithMessage("*ForwardLimit*");
    }
}
