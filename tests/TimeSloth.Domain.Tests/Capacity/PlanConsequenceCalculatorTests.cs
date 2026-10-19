namespace TimeSloth.Domain.Tests.Capacity;

// The pure half of ADR-0036: given blocks before and after, capacity and the
// blocks that count toward the band, what changed for one person. The calendar
// here is Mon–Fri at 8 h, with whatever closure a test adds.
public class PlanConsequenceCalculatorTests
{
    private static readonly Guid Resource = Guid.NewGuid();
    private static readonly Guid Node = Guid.NewGuid();
    private static readonly Guid Root = Guid.NewGuid();
    private static readonly Guid OtherNode = Guid.NewGuid();
    private static readonly Guid OtherRoot = Guid.NewGuid();

    private static readonly Dictionary<Guid, Guid> Roots = new() { [Node] = Root, [OtherNode] = OtherRoot };
    private static readonly HashSet<Guid> GestureRoots = [Root];

    private static DateOnly D(int month, int day) => new(2026, month, day);

    private static Allocation Block(DateOnly from, DateOnly to, decimal percent, Guid? node = null) =>
        Allocation.CreateCoverage(Guid.NewGuid(), node ?? Node, Resource, from, to, percent);

    // The same block with a different end: what a re-snap produces.
    private static BlockShift EndMoved(Allocation before, DateOnly newEnd)
    {
        var after = Allocation.CreateCoverage(
            before.DemandId, before.ProjectNodeId, before.ResourceId, before.PeriodStart, newEnd, before.AllocationPercent);
        return new BlockShift(before, after);
    }

    private static Func<DateOnly, TimeSpan> WorkWeek(params (DateOnly From, DateOnly To)[] closed) => d =>
        d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || closed.Any(c => c.From <= d && d <= c.To)
            ? TimeSpan.Zero
            : TimeSpan.FromHours(8);

    private static PersonConsequence Price(
        BlockShift shift,
        Func<DateOnly, TimeSpan> capacityOn,
        IReadOnlyCollection<Allocation>? others = null,
        IReadOnlyCollection<(DateOnly, DateOnly, string)>? closures = null,
        DateOnly? availableUntil = null)
    {
        others ??= [];
        return PlanConsequenceCalculator.ForPerson(
            [shift],
            [shift.Before, .. others],
            [shift.After, .. others],
            capacityOn,
            Roots,
            GestureRoots,
            closures ?? [],
            availableFrom: null,
            availableUntil);
    }

    [Fact]
    public void ChangedDays_AreTheDaysInExactlyOneWindow()
    {
        var shift = EndMoved(Block(D(7, 1), D(8, 30), 60m), D(9, 2));

        var days = PlanConsequenceCalculator.ChangedDays(shift).ToList();

        days.Should().Equal((D(8, 31), true), (D(9, 1), true), (D(9, 2), true));
    }

    [Fact]
    public void HoursDelta_IsPercentTimesCapacity_OverTheWorkingDaysGained()
    {
        // 31 Aug – 9 Sep 2026 is ten calendar days and EIGHT working days.
        var shift = EndMoved(Block(D(7, 1), D(8, 30), 60m), D(9, 9));

        PlanConsequenceCalculator.HoursDelta(shift, WorkWeek()).Should().Be(TimeSpan.FromHours(38.4));
    }

    [Fact]
    public void HoursDelta_IsNegative_WhenTheBlockShrinks()
    {
        // 17 – 30 Aug: ten working days at 80%.
        var shift = EndMoved(Block(D(7, 1), D(8, 30), 80m), D(8, 16));

        PlanConsequenceCalculator.HoursDelta(shift, WorkWeek()).Should().Be(TimeSpan.FromHours(-64));
    }

    [Fact]
    public void AWeekendInsideTheRun_DoesNotSplitTheInterval()
    {
        var shift = EndMoved(Block(D(7, 1), D(8, 30), 60m), D(9, 9));

        var person = Price(shift, WorkWeek());

        person.Intervals.Should().ContainSingle().Which.Should().Match<LoadInterval>(i =>
            i.From == D(8, 31) && i.To == D(9, 9) && i.PercentBefore == 0m && i.PercentAfter == 60m);
    }

    [Fact]
    public void TheRateIsSummedAcrossProjects_AndTheOtherProjectIsNamed()
    {
        var shift = EndMoved(Block(D(7, 1), D(8, 30), 80m), D(9, 9));
        var elsewhere = Block(D(8, 1), D(9, 30), 40m, OtherNode);

        var person = Price(shift, WorkWeek(), others: [elsewhere]);

        var interval = person.Intervals.Should().ContainSingle().Subject;
        interval.PercentBefore.Should().Be(40m);
        interval.PercentAfter.Should().Be(120m);
        interval.OtherRootIds.Should().Equal(OtherRoot);
    }

    [Fact]
    public void ARateThatChangesHalfWay_OpensASecondInterval()
    {
        var shift = EndMoved(Block(D(7, 1), D(8, 30), 60m), D(9, 9));
        var elsewhere = Block(D(9, 7), D(9, 30), 30m, OtherNode);

        var person = Price(shift, WorkWeek(), others: [elsewhere]);

        person.Intervals.Should().HaveCount(2);
        person.Intervals[0].Should().Match<LoadInterval>(i => i.To == D(9, 4) && i.PercentAfter == 60m);
        person.Intervals[1].Should().Match<LoadInterval>(i =>
            i.From == D(9, 7) && i.PercentBefore == 30m && i.PercentAfter == 90m);
    }

    [Fact]
    public void ADayWithoutCapacity_HasNoUtilization_SoItIsNeitherZeroNorOverload()
    {
        // Fase 3 stretches from 25 Sep to 9 Oct across a closure on 5–9 Oct:
        // the interval stops at the last day that has capacity.
        var closure = (D(10, 5), D(10, 9));
        var shift = EndMoved(Block(D(8, 31), D(9, 25), 60m), D(10, 9));

        var person = Price(shift, WorkWeek(closure), closures: [(closure.Item1, closure.Item2, "Trasloco")]);

        person.Intervals.Should().ContainSingle().Which.Should().Match<LoadInterval>(i =>
            i.From == D(9, 28) && i.To == D(10, 2));
        person.HoursDelta.Should().Be(TimeSpan.FromHours(24)); // 5 days x 60% x 8 h
        person.ZeroCapacity.Should().ContainSingle().Which.Should().Be(
            new ZeroCapacityRun(D(10, 5), D(10, 9), "Trasloco"));
    }

    [Fact]
    public void AWeekend_IsZeroCapacityToo_AndIsNotReportedAsARun()
    {
        var shift = EndMoved(Block(D(7, 1), D(8, 30), 60m), D(9, 9));

        Price(shift, WorkWeek()).ZeroCapacity.Should().BeEmpty();
    }

    [Fact]
    public void HoursPastTheAvailability_AreCounted_AndCarriedSeparately()
    {
        // Available until 30 Sep; the stretch adds 1 and 2 Oct before a closure.
        var closure = (D(10, 5), D(10, 9));
        var shift = EndMoved(Block(D(8, 31), D(9, 25), 60m), D(10, 9));

        var person = Price(shift, WorkWeek(closure), availableUntil: D(9, 30));

        person.Beyond.Should().Be(new BeyondAvailability(D(10, 1), D(10, 2), 2, TimeSpan.FromHours(9.6)));
        // They are still in the total: capacity does not read availability.
        person.HoursDelta.Should().Be(TimeSpan.FromHours(24));
    }

    [Fact]
    public void ABlockThatDoesNotCountTowardTheBand_MovesNoInterval()
    {
        // The caller narrows the band basis; here the moved block is left out
        // of it, as a tentative block is.
        var shift = EndMoved(Block(D(7, 1), D(8, 30), 60m), D(9, 9));

        var person = PlanConsequenceCalculator.ForPerson(
            [shift], [], [], WorkWeek(), Roots, GestureRoots, [], null, null);

        person.Intervals.Should().BeEmpty();
        person.HoursDelta.Should().Be(TimeSpan.FromHours(38.4));
    }
}
