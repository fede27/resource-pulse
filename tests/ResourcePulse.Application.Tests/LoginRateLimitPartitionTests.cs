using System.Net;
using ResourcePulse.Http.Auth;

namespace ResourcePulse.Application.Tests;

// The key the login throttle buckets on (review finding 6). What matters is that
// two requests from one caller land in the SAME bucket and two callers do not —
// a key an attacker can vary at will is not a limit at all.
public class LoginRateLimitPartitionTests
{
    private static string Key(string address) => LoginRateLimiting.PartitionKeyFor(IPAddress.Parse(address));

    [Fact]
    public void AnAddressLessConnection_FallsIntoOneSharedBucket()
    {
        LoginRateLimiting.PartitionKeyFor(null).Should().Be("unknown");
    }

    [Fact]
    public void IPv4_IsTheKey()
    {
        Key("203.0.113.7").Should().Be("203.0.113.7");
        Key("203.0.113.7").Should().NotBe(Key("203.0.113.8"));
    }

    [Fact]
    public void AnIPv4MappedAddress_SharesTheBucketOfThePlainIPv4()
    {
        // Kestrel hands back ::ffff:203.0.113.7 on a dual-stack socket. Same caller,
        // so it must not get a second allowance by connecting differently.
        Key("::ffff:203.0.113.7").Should().Be(Key("203.0.113.7"));
    }

    [Fact]
    public void IPv6_CollapsesToTheSixtyFour()
    {
        // One subscriber is routinely handed a whole /64. Keyed on the full /128,
        // walking through it would mint a fresh allowance per request.
        var first = Key("2001:db8:abcd:1234::1");
        var later = Key("2001:db8:abcd:1234:ffff:ffff:ffff:ffff");

        first.Should().Be(later);
        first.Should().Be("2001:db8:abcd:1234::/64");
    }

    [Fact]
    public void DifferentSixtyFours_AreDifferentCallers()
    {
        Key("2001:db8:abcd:1234::1").Should().NotBe(Key("2001:db8:abcd:1235::1"));
    }
}
