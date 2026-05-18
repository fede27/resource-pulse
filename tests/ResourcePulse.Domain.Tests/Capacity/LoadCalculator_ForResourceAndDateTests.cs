namespace ResourcePulse.Domain.Tests.Capacity;

public class LoadCalculator_ForResourceAndDateTests
{
    private static readonly Guid R1 = Guid.NewGuid();
    private static readonly Guid R2 = Guid.NewGuid();
    private static readonly Guid N1 = Guid.NewGuid();
    private static readonly Guid N2 = Guid.NewGuid();
    private static readonly DateOnly D = new(2026, 6, 1);

    [Fact]
    public void NoAllocations_ReturnsZero()
    {
        var hours = LoadCalculator.ForResourceAndDate(R1, [], TimeSpan.FromHours(8), D);
        hours.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void SingleAllocation_50Percent_Returns4h()
    {
        var a = Allocation.Create(R1, N1, D, D.AddDays(7), 50m);

        var hours = LoadCalculator.ForResourceAndDate(R1, [a], TimeSpan.FromHours(8), D);

        hours.Should().Be(TimeSpan.FromHours(4));
    }

    [Fact]
    public void SingleAllocation_100Percent_ReturnsFullCapacity()
    {
        var a = Allocation.Create(R1, N1, D, D, 100m);

        var hours = LoadCalculator.ForResourceAndDate(R1, [a], TimeSpan.FromHours(8), D);

        hours.Should().Be(TimeSpan.FromHours(8));
    }

    [Fact]
    public void TwoOverlappingAllocations_Overallocates()
    {
        // Two 60% on same day vs 8h capacity -> 9.6h
        var a1 = Allocation.Create(R1, N1, D, D, 60m);
        var a2 = Allocation.Create(R1, N2, D, D, 60m);

        var hours = LoadCalculator.ForResourceAndDate(R1, [a1, a2], TimeSpan.FromHours(8), D);

        hours.Should().Be(TimeSpan.FromHours(9.6));
    }

    [Fact]
    public void OtherResource_Ignored()
    {
        var mine = Allocation.Create(R1, N1, D, D, 50m);
        var theirs = Allocation.Create(R2, N1, D, D, 100m);

        var hours = LoadCalculator.ForResourceAndDate(R1, [mine, theirs], TimeSpan.FromHours(8), D);

        hours.Should().Be(TimeSpan.FromHours(4));
    }

    [Fact]
    public void DateOutsidePeriod_NotCounted()
    {
        var a = Allocation.Create(R1, N1, new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 20), 100m);

        var hours = LoadCalculator.ForResourceAndDate(R1, [a], TimeSpan.FromHours(8), D);

        hours.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void DateOnPeriodStart_Inclusive()
    {
        var a = Allocation.Create(R1, N1, D, D.AddDays(5), 50m);

        var hours = LoadCalculator.ForResourceAndDate(R1, [a], TimeSpan.FromHours(8), D);

        hours.Should().Be(TimeSpan.FromHours(4));
    }

    [Fact]
    public void DateOnPeriodEnd_Inclusive()
    {
        var end = D.AddDays(5);
        var a = Allocation.Create(R1, N1, D, end, 50m);

        var hours = LoadCalculator.ForResourceAndDate(R1, [a], TimeSpan.FromHours(8), end);

        hours.Should().Be(TimeSpan.FromHours(4));
    }

    [Fact]
    public void ZeroCapacity_AllocationActive_ReturnsZeroHours()
    {
        var a = Allocation.Create(R1, N1, D, D, 50m);

        var hours = LoadCalculator.ForResourceAndDate(R1, [a], TimeSpan.Zero, D);

        hours.Should().Be(TimeSpan.Zero);
    }
}
