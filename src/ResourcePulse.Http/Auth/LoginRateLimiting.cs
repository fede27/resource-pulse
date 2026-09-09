using System.Net;
using System.Net.Sockets;

namespace ResourcePulse.Http.Auth;

/// <summary>
/// Names the rate-limit policy guarding the anonymous login endpoints (ADR-0031).
/// </summary>
/// <remarks>
/// Named here, next to the controller that consumes it, and configured in
/// <c>ResourcePulse.Hosting</c> — the same split as <see cref="AccessPolicies"/>.
/// <para>
/// It is not redundant with Zitadel's own account lockout. Lockout protects one
/// account against many guesses; this protects the <b>instance</b> against one
/// caller spraying one password across many accounts, which never trips a
/// per-account counter. Until this endpoint existed the application had no
/// unauthenticated surface at all, so it also had nothing to throttle.
/// </para>
/// </remarks>
public static class LoginRateLimiting
{
    public const string PolicyName = "login";

    /// <summary>
    /// The bucket a caller falls into. Lives with the policy name rather than with
    /// the wiring because it is part of what the policy MEANS.
    /// </summary>
    /// <remarks>
    /// Takes the address the pipeline settled on — the socket peer, or the client
    /// the trusted proxy named once <c>ForwardedHeaders</c> is enabled. It never
    /// reads a header itself: a key the caller can choose is not a limit.
    /// <para>
    /// IPv6 collapses to the /64. A single subscriber is routinely handed one, so
    /// keying on the full /128 would let anyone with an allocation mint a fresh
    /// bucket per request — the same evasion as trusting the header, by another
    /// route.
    /// </para>
    /// </remarks>
    public static string PartitionKeyFor(IPAddress? address)
    {
        if (address is null) return "unknown";

        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily != AddressFamily.InterNetworkV6) return address.ToString();

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"{new IPAddress(bytes)}/64";
    }
}
