using ResourcePulse.Domain.Configuration;

namespace ResourcePulse.Domain.Tests.Configuration;

public class TimeFenceConfigurationTests
{
    private static readonly DateOnly Today = new(2026, 6, 24);

    // ── Boundaries: rolling projection from "today" ───────────────────────────

    [Fact]
    public void FrozenTwoWeeks_ProducesExpectedRollingBoundaries()
    {
        // frozen = 2 weeks, slushy = 2 months.
        var config = TimeFenceConfiguration.Create(
            Guid.NewGuid(),
            Duration.Of(2, DurationUnit.Weeks),
            Duration.Of(2, DurationUnit.Months));

        var b = config.ComputeBoundaries(Today);

        b.FrozenUntil.Should().Be(new DateOnly(2026, 7, 8));   // +14 days
        b.SlushyUntil.Should().Be(new DateOnly(2026, 8, 24));  // +2 months
    }

    [Theory]
    [InlineData(2026, 6, 24, FenceZone.Frozen)]   // today
    [InlineData(2026, 7, 8, FenceZone.Frozen)]    // frozen boundary inclusive
    [InlineData(2026, 7, 9, FenceZone.Slushy)]    // just past frozen
    [InlineData(2026, 8, 24, FenceZone.Slushy)]   // slushy boundary inclusive
    [InlineData(2026, 8, 25, FenceZone.Liquid)]   // just past slushy
    [InlineData(2027, 1, 1, FenceZone.Liquid)]
    public void ZoneFor_ClassifiesDatesAcrossTheThreeZones(int y, int m, int d, FenceZone expected)
    {
        var config = TimeFenceConfiguration.Create(
            Guid.NewGuid(),
            Duration.Of(2, DurationUnit.Weeks),
            Duration.Of(2, DurationUnit.Months));

        config.ZoneFor(new DateOnly(y, m, d), Today).Should().Be(expected);
    }

    // ── Mixed units normalize and validate ────────────────────────────────────

    [Fact]
    public void MixedUnits_FrozenWeeksSlushyMonths_AreAccepted()
    {
        var act = () => TimeFenceConfiguration.Create(
            Guid.NewGuid(),
            Duration.Of(3, DurationUnit.Weeks),   // 21 days
            Duration.Of(1, DurationUnit.Months)); // ~30 days
        act.Should().NotThrow();
    }

    [Fact]
    public void FrozenNotShorterThanSlushy_IsRejected()
    {
        // 2 months (~60d) frozen vs 6 weeks (42d) slushy → frozen ≥ slushy.
        var act = () => TimeFenceConfiguration.Create(
            Guid.NewGuid(),
            Duration.Of(2, DurationUnit.Months),
            Duration.Of(6, DurationUnit.Weeks));
        act.Should().Throw<DomainException>().WithMessage("*strictly shorter*");
    }

    [Fact]
    public void EqualNormalizedHorizons_AreRejected()
    {
        // 14 days vs 2 weeks = identical length.
        var act = () => TimeFenceConfiguration.Create(
            Guid.NewGuid(),
            Duration.Of(14, DurationUnit.Days),
            Duration.Of(2, DurationUnit.Weeks));
        act.Should().Throw<DomainException>().WithMessage("*strictly shorter*");
    }

    // ── Rolling: different "today" gives different boundaries ──────────────────

    [Fact]
    public void Rolling_BoundariesShiftWithToday()
    {
        var config = TimeFenceConfiguration.CreateDefault();

        var b1 = config.ComputeBoundaries(new DateOnly(2026, 1, 1));
        var b2 = config.ComputeBoundaries(new DateOnly(2026, 6, 1));

        b1.FrozenUntil.Should().NotBe(b2.FrozenUntil);
        b1.SlushyUntil.Should().NotBe(b2.SlushyUntil);
        b1.FrozenUntil.Should().Be(new DateOnly(2026, 1, 15));
        b2.FrozenUntil.Should().Be(new DateOnly(2026, 6, 15));
    }

    [Fact]
    public void Duration_RejectsNonPositiveValue()
    {
        var act = () => Duration.Of(0, DurationUnit.Days);
        act.Should().Throw<DomainException>().WithMessage("*positive integer*");
    }
}
