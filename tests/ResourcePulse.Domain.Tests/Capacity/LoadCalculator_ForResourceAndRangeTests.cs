namespace ResourcePulse.Domain.Tests.Capacity;

public class LoadCalculator_ForResourceAndRangeTests
{
    private static readonly Guid R1 = Guid.NewGuid();
    private static readonly Guid N1 = Guid.NewGuid();
    private static readonly DateOnly Mon = new(2026, 6, 1);
    private static readonly DateOnly Fri = new(2026, 6, 5);

    private static Dictionary<DateOnly, TimeSpan> Workweek8h()
    {
        var d = new Dictionary<DateOnly, TimeSpan>();
        for (var dt = Mon; dt <= Fri; dt = dt.AddDays(1))
            d[dt] = TimeSpan.FromHours(8);
        return d;
    }

    [Fact]
    public void FromAfterTo_YieldsNothing()
    {
        var result = LoadCalculator.ForResourceAndRange(
            R1, [], new Dictionary<DateOnly, TimeSpan>(),
            from: Fri, toInclusive: Mon).ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void NoAllocations_AllZeros()
    {
        var capacity = Workweek8h();

        var result = LoadCalculator.ForResourceAndRange(
            R1, [], capacity, Mon, Fri).ToList();

        result.Should().HaveCount(5);
        result.Should().AllSatisfy(d =>
        {
            d.Hours.Should().Be(TimeSpan.Zero);
            d.LoadPercent.Should().Be(0m);
        });
    }

    [Fact]
    public void SingleAllocation_PartialRange_LoadOnlyOnCoveredDates()
    {
        // Allocation covers Wed-Thu at 50%; Mon, Tue, Fri have 0 load.
        var a = Allocation.Create(R1, N1, Mon.AddDays(2), Mon.AddDays(3), 50m);
        var capacity = Workweek8h();

        var result = LoadCalculator.ForResourceAndRange(
            R1, [a], capacity, Mon, Fri).ToList();

        result[0].Hours.Should().Be(TimeSpan.Zero); // Mon
        result[1].Hours.Should().Be(TimeSpan.Zero); // Tue
        result[2].Hours.Should().Be(TimeSpan.FromHours(4)); // Wed
        result[3].Hours.Should().Be(TimeSpan.FromHours(4)); // Thu
        result[4].Hours.Should().Be(TimeSpan.Zero); // Fri
    }

    [Fact]
    public void AllocationExtendingBeyondRange_OnlyConsidersRequestedDates()
    {
        // Allocation: May 1 - Dec 31, 100%. Query Mon-Fri only.
        var a = Allocation.Create(R1, N1, new DateOnly(2026, 5, 1), new DateOnly(2026, 12, 31), 100m);
        var capacity = Workweek8h();

        var result = LoadCalculator.ForResourceAndRange(R1, [a], capacity, Mon, Fri).ToList();

        result.Should().HaveCount(5);
        result.Should().AllSatisfy(d => d.Hours.Should().Be(TimeSpan.FromHours(8)));
    }

    [Fact]
    public void LoadPercent_100Percent()
    {
        var a = Allocation.Create(R1, N1, Mon, Fri, 100m);
        var capacity = Workweek8h();

        var result = LoadCalculator.ForResourceAndRange(R1, [a], capacity, Mon, Fri).ToList();

        result.Should().AllSatisfy(d => d.LoadPercent.Should().Be(100m));
    }

    [Fact]
    public void LoadPercent_50Percent()
    {
        var a = Allocation.Create(R1, N1, Mon, Fri, 50m);
        var capacity = Workweek8h();

        var result = LoadCalculator.ForResourceAndRange(R1, [a], capacity, Mon, Fri).ToList();

        result.Should().AllSatisfy(d => d.LoadPercent.Should().Be(50m));
    }

    [Fact]
    public void LoadPercent_Over100_Overallocated()
    {
        // Two 75% allocations on different nodes = 150% load.
        var n2 = Guid.NewGuid();
        var a1 = Allocation.Create(R1, N1, Mon, Fri, 75m);
        var a2 = Allocation.Create(R1, n2, Mon, Fri, 75m);
        var capacity = Workweek8h();

        var result = LoadCalculator.ForResourceAndRange(R1, [a1, a2], capacity, Mon, Fri).ToList();

        result.Should().AllSatisfy(d => d.LoadPercent.Should().Be(150m));
    }

    [Fact]
    public void LoadPercent_ZeroCapacityNonZeroAllocation_ReturnsMaxValueSentinel()
    {
        // Weekend day with 0 capacity, but allocation is active.
        var capacity = new Dictionary<DateOnly, TimeSpan> { [Mon] = TimeSpan.Zero };
        var a = Allocation.Create(R1, N1, Mon, Mon, 50m);

        var result = LoadCalculator.ForResourceAndRange(R1, [a], capacity, Mon, Mon).ToList();

        result.Should().ContainSingle();
        result[0].Hours.Should().Be(TimeSpan.Zero);
        result[0].LoadPercent.Should().Be(decimal.MaxValue);
    }

    [Fact]
    public void MissingCapacityDate_TreatedAsZero()
    {
        var capacity = new Dictionary<DateOnly, TimeSpan>(); // empty
        var a = Allocation.Create(R1, N1, Mon, Mon, 50m);

        var result = LoadCalculator.ForResourceAndRange(R1, [a], capacity, Mon, Mon).ToList();

        result[0].Hours.Should().Be(TimeSpan.Zero);
        result[0].LoadPercent.Should().Be(decimal.MaxValue); // sentinel: load, zero capacity
    }

    [Fact]
    public void Determinism_SameInputsProduceSameOutput()
    {
        var a1 = Allocation.Create(R1, N1, Mon, Fri, 30m);
        var a2 = Allocation.Create(R1, Guid.NewGuid(), Mon, Fri, 40m);
        var capacity = Workweek8h();

        var first  = LoadCalculator.ForResourceAndRange(R1, [a1, a2], capacity, Mon, Fri).ToList();
        var second = LoadCalculator.ForResourceAndRange(R1, [a2, a1], capacity, Mon, Fri).ToList();

        first.Should().BeEquivalentTo(second);
    }
}
