namespace ResourcePulse.Domain.Tests.Capacity;

public class AllocationResolverTests
{
    // ── PercentForHours: happy paths ────────────────────────────────────────

    [Fact]
    public void PercentForHours_HalfOfCapacity_Returns50()
    {
        var p = AllocationResolver.PercentForHours(
            TimeSpan.FromHours(40), TimeSpan.FromHours(80));

        p.Should().Be(50.00m);
    }

    [Fact]
    public void PercentForHours_AllOfCapacity_Returns100()
    {
        var p = AllocationResolver.PercentForHours(
            TimeSpan.FromHours(80), TimeSpan.FromHours(80));

        p.Should().Be(100.00m);
    }

    [Fact]
    public void PercentForHours_QuarterOfCapacity_Returns25()
    {
        // 20h over a 80h window
        var p = AllocationResolver.PercentForHours(
            TimeSpan.FromHours(20), TimeSpan.FromHours(80));

        p.Should().Be(25.00m);
    }

    [Fact]
    public void PercentForHours_ExceedingCapacity_ReturnsAbove100()
    {
        // 120h over a 80h window -> 150% (overcommitment is legal output)
        var p = AllocationResolver.PercentForHours(
            TimeSpan.FromHours(120), TimeSpan.FromHours(80));

        p.Should().Be(150.00m);
    }

    [Fact]
    public void PercentForHours_LargeOvercommitment_ReturnsCorrectPercent()
    {
        // 800h over a 80h window -> 1000%. At the new cap; AllocationService
        // is responsible for rejecting; the resolver itself does not cap.
        var p = AllocationResolver.PercentForHours(
            TimeSpan.FromHours(800), TimeSpan.FromHours(80));

        p.Should().Be(1000.00m);
    }

    [Fact]
    public void PercentForHours_OneHourOverThree_RoundsToTwoDecimals()
    {
        // 1h / 3h * 100 = 33.333... -> 33.33 (banker's rounding at midpoint
        // doesn't apply here — strict round-down)
        var p = AllocationResolver.PercentForHours(
            TimeSpan.FromHours(1), TimeSpan.FromHours(3));

        p.Should().Be(33.33m);
    }

    [Fact]
    public void PercentForHours_TwoHoursOverThree_RoundsToTwoDecimals()
    {
        // 2h / 3h * 100 = 66.666... -> 66.67
        var p = AllocationResolver.PercentForHours(
            TimeSpan.FromHours(2), TimeSpan.FromHours(3));

        p.Should().Be(66.67m);
    }

    [Fact]
    public void PercentForHours_BankersRoundingAtMidpoint()
    {
        // Construct a value with exactly .005 to verify banker's rounding (ToEven):
        // 0.5h / 200h * 100 = 0.25 (exact, no rounding test here — try midpoints).
        // Better: 1.0h / 80h * 100 = 1.25 — no midpoint.
        // For a true .005 midpoint, take (0.005h * 100/100) — but TimeSpan.Ticks
        // arithmetic means we can craft one via raw ticks.
        // 12345 ticks / 100 ticks * 100 = 12345% — not useful.
        // Instead: pick a ratio that hits .015 exactly: 3/200 * 100 = 1.5 → no.
        // .005 case: 1/200 = .005 → 0.50%. Round to ToEven from .005 → 0.50.
        var p1 = AllocationResolver.PercentForHours(
            TimeSpan.FromHours(1), TimeSpan.FromHours(200));
        p1.Should().Be(0.50m); // exact, no rounding decision

        // Hit a true tie: 0.015 percent. 3h / 20000h * 100 = 0.015. ToEven -> .02.
        var p2 = AllocationResolver.PercentForHours(
            TimeSpan.FromHours(3), TimeSpan.FromHours(20000));
        p2.Should().Be(0.02m); // 0.015 -> 0.02 (round to even, 0.02 is even hundredths)

        // 0.025 -> 0.02 (tie, even side is .02)
        var p3 = AllocationResolver.PercentForHours(
            TimeSpan.FromHours(5), TimeSpan.FromHours(20000));
        p3.Should().Be(0.02m); // 0.025 -> 0.02 (banker's rounding)
    }

    // ── PercentForHours: validation ────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PercentForHours_NonPositiveHours_Throws(int hours)
    {
        var act = () => AllocationResolver.PercentForHours(
            TimeSpan.FromHours(hours), TimeSpan.FromHours(80));

        act.Should().Throw<DomainException>().WithMessage("*targetHours must be greater than zero*");
    }

    [Fact]
    public void PercentForHours_ZeroCapacity_Throws()
    {
        var act = () => AllocationResolver.PercentForHours(
            TimeSpan.FromHours(10), TimeSpan.Zero);

        act.Should().Throw<DomainException>().WithMessage("*zero capacity*");
    }

    [Fact]
    public void PercentForHours_NegativeCapacity_Throws()
    {
        var act = () => AllocationResolver.PercentForHours(
            TimeSpan.FromHours(10), TimeSpan.FromHours(-5));

        act.Should().Throw<DomainException>().WithMessage("*zero capacity*");
    }

    // ── HoursForPercent: happy paths ───────────────────────────────────────

    [Fact]
    public void HoursForPercent_50PercentOf80h_Returns40h()
    {
        var h = AllocationResolver.HoursForPercent(50m, TimeSpan.FromHours(80));

        h.Should().Be(TimeSpan.FromHours(40));
    }

    [Fact]
    public void HoursForPercent_100Percent_ReturnsFullCapacity()
    {
        var h = AllocationResolver.HoursForPercent(100m, TimeSpan.FromHours(80));

        h.Should().Be(TimeSpan.FromHours(80));
    }

    [Fact]
    public void HoursForPercent_AboveCapacity_NoClampingBydDesign()
    {
        // 150% of 80h = 120h. The resolver does not clamp; the resulting
        // hours exceed the window capacity, which is the overcommitment signal.
        var h = AllocationResolver.HoursForPercent(150m, TimeSpan.FromHours(80));

        h.Should().Be(TimeSpan.FromHours(120));
    }

    [Fact]
    public void HoursForPercent_ZeroCapacity_ReturnsZero()
    {
        // Zero capacity is a legal input here (callers gate it themselves
        // when zero is meaningful). Truthful answer: percent of nothing = nothing.
        var h = AllocationResolver.HoursForPercent(50m, TimeSpan.Zero);

        h.Should().Be(TimeSpan.Zero);
    }

    // ── HoursForPercent: validation ────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-50)]
    public void HoursForPercent_NonPositivePercent_Throws(double percent)
    {
        var act = () => AllocationResolver.HoursForPercent(
            (decimal)percent, TimeSpan.FromHours(80));

        act.Should().Throw<DomainException>().WithMessage("*percent must be greater than zero*");
    }

    // ── Round-trip ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(40, 80)]    // clean half
    [InlineData(20, 80)]    // clean quarter
    [InlineData(120, 80)]   // overcommitment (150%)
    [InlineData(80, 80)]    // 100%
    [InlineData(1, 8)]      // 12.5% — clean
    [InlineData(8, 40)]     // 20% — clean
    public void RoundTrip_HoursToPercentToHours_Reproduces(double targetHours, double capacityHours)
    {
        var target = TimeSpan.FromHours(targetHours);
        var capacity = TimeSpan.FromHours(capacityHours);

        var percent = AllocationResolver.PercentForHours(target, capacity);
        var back = AllocationResolver.HoursForPercent(percent, capacity);

        back.Should().Be(target);
    }

    [Fact]
    public void RoundTrip_IrrationalRatio_WithinTolerance()
    {
        // 1h / 3h percent quantizes to 33.33. Round-tripping that against 3h
        // capacity yields 3h * 0.3333 = 0.9999h = 59.994 minutes.
        // Expected divergence: capacity * 0.005 / 100 = 3h * 0.00005 = 0.54s.
        // Spec quotes "within ε" — assert under a generous 1 minute bound.
        var target = TimeSpan.FromHours(1);
        var capacity = TimeSpan.FromHours(3);

        var percent = AllocationResolver.PercentForHours(target, capacity);
        var back = AllocationResolver.HoursForPercent(percent, capacity);

        var delta = (target - back).Duration();
        delta.Should().BeLessThan(TimeSpan.FromMinutes(1));
        // And specifically: at most capacity * (0.005/100) = 0.54 seconds
        delta.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(0.25, 80)]   // 0.25h on 80h capacity -> 0.31% (rounded)
    [InlineData(1, 1000)]    // 1h on 1000h capacity -> 0.10%
    [InlineData(100, 1)]     // 10000% on tiny capacity (100h target / 1h cap)
    public void EdgeCases_DoNotThrow(double targetHours, double capacityHours)
    {
        var percent = AllocationResolver.PercentForHours(
            TimeSpan.FromHours(targetHours), TimeSpan.FromHours(capacityHours));

        percent.Should().BeGreaterThan(0m);
        // Round-trip lands within a generous tolerance regardless of magnitude.
        var back = AllocationResolver.HoursForPercent(percent, TimeSpan.FromHours(capacityHours));
        ((TimeSpan.FromHours(targetHours) - back).Duration())
            .Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Determinism_SameInputsProduceSameOutput()
    {
        var p1 = AllocationResolver.PercentForHours(TimeSpan.FromHours(37), TimeSpan.FromHours(83));
        var p2 = AllocationResolver.PercentForHours(TimeSpan.FromHours(37), TimeSpan.FromHours(83));
        p1.Should().Be(p2);

        var h1 = AllocationResolver.HoursForPercent(44.578m, TimeSpan.FromHours(83));
        var h2 = AllocationResolver.HoursForPercent(44.578m, TimeSpan.FromHours(83));
        h1.Should().Be(h2);
    }
}
