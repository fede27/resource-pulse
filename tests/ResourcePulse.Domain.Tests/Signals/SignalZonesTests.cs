using ResourcePulse.Domain.Configuration;
using ResourcePulse.Domain.Signals;

namespace ResourcePulse.Domain.Tests.Signals;

public class SignalZonesTests
{
    private static readonly DateOnly Today = new(2026, 6, 24);

    // frozen = 2 weeks (→ 8 Jul), slushy = 2 months (→ 24 Aug).
    private static FenceBoundaries Boundaries() =>
        TimeFenceConfiguration
            .Create(Guid.NewGuid(), Duration.Of(2, DurationUnit.Weeks), Duration.Of(2, DurationUnit.Months))
            .ComputeBoundaries(Today);

    // ── Resolution ────────────────────────────────────────────────────────────

    [Fact]
    public void NoDeadline_ResolvesToNoZone()
    {
        // Hygiene and slack have no deadline. Saying so is the point: the
        // prototype hardcodes 'frozen' on hygiene rows, inventing urgency.
        SignalZones.Resolve(deadline: null, Today, Boundaries()).Should().BeNull();
    }

    [Fact]
    public void PastDeadline_IsOverdue_NotClampedToFrozen()
    {
        // ADR-0033 §6: a missed deadline must outrank a frozen one. Clamping past
        // dates to today (as the prototype does) flattens the distinction the
        // ranking exists to make.
        SignalZones.Resolve(Today.AddDays(-1), Today, Boundaries())
            .Should().Be(SignalZone.Overdue);
    }

    [Theory]
    [InlineData(2026, 6, 24, SignalZone.Frozen)]   // today
    [InlineData(2026, 7, 8, SignalZone.Frozen)]    // frozen boundary, inclusive
    [InlineData(2026, 7, 9, SignalZone.Slushy)]
    [InlineData(2026, 8, 24, SignalZone.Slushy)]   // slushy boundary, inclusive
    [InlineData(2026, 8, 25, SignalZone.Liquid)]
    public void FutureDeadline_FollowsTheFenceBoundaries(int y, int m, int d, SignalZone expected) =>
        SignalZones.Resolve(new DateOnly(y, m, d), Today, Boundaries()).Should().Be(expected);

    // ── Admission (ADR-0033 §8) ───────────────────────────────────────────────

    [Theory]
    [InlineData(null, true)]                  // no deadline: admitted, ranks on tier
    [InlineData(SignalZone.Overdue, true)]
    [InlineData(SignalZone.Frozen, true)]
    [InlineData(SignalZone.Slushy, true)]
    [InlineData(SignalZone.Liquid, false)]    // beyond the committing horizon
    public void CommittingHorizon_AdmitsEverythingButLiquid(SignalZone? zone, bool admitted) =>
        SignalZones.IsInCommittingHorizon(zone).Should().Be(admitted);

    // ── Worsening (ADR-0032 §6) ───────────────────────────────────────────────

    [Fact]
    public void TentativeEnteringTheFrozenZone_Worsens()
    {
        // The prototype's own example: the fence rolled and the tentative crossed
        // it. This one must speak.
        SignalZones.IsWorsening(SignalZone.Slushy, SignalZone.Frozen).Should().BeTrue();
    }

    [Fact]
    public void BreachSlidingFurtherOut_DoesNotWorsen()
    {
        // "Non voglio rumore": an overcommit whose breaching segment moves two
        // weeks later relaxes. The data is still updated — only the feed is quiet.
        SignalZones.IsWorsening(SignalZone.Frozen, SignalZone.Slushy).Should().BeFalse();
    }

    [Fact]
    public void GainingADeadline_Worsens_LosingOne_DoesNot()
    {
        SignalZones.IsWorsening(null, SignalZone.Liquid).Should().BeTrue();
        SignalZones.IsWorsening(SignalZone.Frozen, null).Should().BeFalse();
    }

    [Fact]
    public void SameZone_DoesNotWorsen() =>
        SignalZones.IsWorsening(SignalZone.Frozen, SignalZone.Frozen).Should().BeFalse();
}
