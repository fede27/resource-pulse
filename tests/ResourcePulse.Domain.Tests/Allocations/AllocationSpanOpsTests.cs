using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Events;

namespace ResourcePulse.Domain.Tests.Allocations;

// Span operations (ADR-0017): SplitAt, ChangeRateFrom, Shift, Resize, plus the
// "tolgo a metà" composition and the gap/overlap properties the cascade relies
// on. The service-level cascade (ShiftFrom) is orchestration of Shift applied
// uniformly; its essential property (gaps preserved, overlap sums) is exercised
// here at the domain level.
public class AllocationSpanOpsTests
{
    private static readonly Guid Resource = Guid.NewGuid();
    private static readonly Guid Node = Guid.NewGuid();
    private static readonly Guid RoleSkill = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly DateOnly Start = new(2026, 6, 1);
    private static readonly DateOnly End = new(2026, 6, 14);

    private static Allocation Fresh(decimal percent = 50m,
        DateOnly? start = null, DateOnly? end = null,
        AllocationStatus status = AllocationStatus.Tentative)
    {
        var a = Allocation.Create(Resource, Node, start ?? Start, end ?? End, percent, "ctx", status);
        a.ClearDomainEvents();
        return a;
    }

    // Per-day rate% sum over a set of blocks (the quantity ADR-0014 makes additive).
    private static decimal RateSumOn(DateOnly date, params Allocation[] blocks) =>
        blocks
            .Where(b => date >= b.PeriodStart && date <= b.PeriodEnd)
            .Sum(b => b.AllocationPercent);

    private static IEnumerable<DateOnly> DaysInclusive(DateOnly from, DateOnly to)
    {
        for (var d = from; d <= to; d = d.AddDays(1)) yield return d;
    }

    // ── SplitAt: sum invariance ───────────────────────────────────────────────

    [Fact]
    public void SplitAt_PreservesRateSum_OnEveryDate()
    {
        var original = Fresh(percent: 60m);
        // Capture the original profile across a window wider than the span.
        var probe = DaysInclusive(Start.AddDays(-2), End.AddDays(2)).ToList();
        var before = probe.ToDictionary(d => d, d => RateSumOn(d, original));

        var splitDate = new DateOnly(2026, 6, 8);
        var second = original.SplitAt(splitDate);

        foreach (var d in probe)
            RateSumOn(d, original, second).Should().Be(before[d],
                $"the split must be equivalent to the original on {d}");
    }

    [Fact]
    public void SplitAt_ProducesAdjacentNonOverlappingBlocks()
    {
        var original = Fresh();
        var splitDate = new DateOnly(2026, 6, 8);

        var second = original.SplitAt(splitDate);

        original.PeriodStart.Should().Be(Start);
        original.PeriodEnd.Should().Be(splitDate.AddDays(-1));   // [start, date-1]
        second.PeriodStart.Should().Be(splitDate);              // [date, end]
        second.PeriodEnd.Should().Be(End);
        // Adjacent, not overlapping: first.End + 1 == second.Start.
        original.PeriodEnd.AddDays(1).Should().Be(second.PeriodStart);
    }

    [Fact]
    public void SplitAt_PreservesRateStatusNodeAndForm()
    {
        var original = Fresh(percent: 70m, status: AllocationStatus.Hard);

        var second = original.SplitAt(new DateOnly(2026, 6, 8));

        second.Id.Should().NotBe(original.Id);
        second.AllocationPercent.Should().Be(70m);
        second.Status.Should().Be(AllocationStatus.Hard);
        second.ProjectNodeId.Should().Be(Node);
        second.ResourceId.Should().Be(Resource);
        second.IsPlaceholder.Should().BeFalse();
        second.Notes.Should().Be("ctx");
    }

    [Fact]
    public void SplitAt_OnPlaceholder_PreservesPlaceholderForm()
    {
        var ph = Allocation.CreatePlaceholder(Node, Start, End, 40m, RoleSkill, Owner);

        var second = ph.SplitAt(new DateOnly(2026, 6, 8));

        second.IsPlaceholder.Should().BeTrue();
        second.ResourceId.Should().BeNull();
        second.RoleSkillId.Should().Be(RoleSkill);
        second.OwnerResourceId.Should().Be(Owner);
        second.AllocationPercent.Should().Be(40m);
    }

    [Fact]
    public void SplitAt_AtLastDay_IsValid()
    {
        var original = Fresh(start: Start, end: new DateOnly(2026, 6, 2)); // 2-day span

        var second = original.SplitAt(new DateOnly(2026, 6, 2)); // date == end

        original.PeriodStart.Should().Be(Start);
        original.PeriodEnd.Should().Be(Start);                  // [d, d]
        second.PeriodStart.Should().Be(new DateOnly(2026, 6, 2));
        second.PeriodEnd.Should().Be(new DateOnly(2026, 6, 2)); // [d+1, d+1]
    }

    [Theory]
    [InlineData("2026-06-01")] // == start  -> first side empty
    [InlineData("2026-05-31")] // <  start
    [InlineData("2026-06-15")] // >  end
    public void SplitAt_OutsideStrictInterior_Throws(string date)
    {
        var original = Fresh();

        var act = () => original.SplitAt(DateOnly.Parse(date));

        act.Should().Throw<DomainException>().WithMessage("*strictly inside*");
    }

    [Fact]
    public void SplitAt_RaisesAllocationSplitEvent_WithProvenance()
    {
        var original = Fresh();
        var splitDate = new DateOnly(2026, 6, 8);

        var second = original.SplitAt(splitDate, "Split");

        original.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AllocationSplit>()
            .Which.Should().Match<AllocationSplit>(e =>
                e.SourceAllocationId == original.Id &&
                e.SplitDate == splitDate &&
                e.NewBlockId == second.Id &&
                e.Reason == "Split");
        // No redundant AllocationPeriodChanged from the source side.
        original.DomainEvents.Should().NotContain(e => e is AllocationPeriodChanged);
    }

    // ── ChangeRateFrom: piecewise-constant profile ────────────────────────────

    [Fact]
    public void ChangeRateFrom_ProducesPiecewiseConstantProfile()
    {
        var original = Fresh(percent: 50m);
        var date = new DateOnly(2026, 6, 8);

        var second = original.ChangeRateFrom(date, 80m);

        // [start, date-1] stays at 50%, [date, end] becomes 80%.
        foreach (var d in DaysInclusive(Start, date.AddDays(-1)))
            RateSumOn(d, original, second).Should().Be(50m);
        foreach (var d in DaysInclusive(date, End))
            RateSumOn(d, original, second).Should().Be(80m);
    }

    [Fact]
    public void ChangeRateFrom_RaisesSplitThenPercentChanged()
    {
        var original = Fresh(percent: 50m);

        var second = original.ChangeRateFrom(new DateOnly(2026, 6, 8), 80m);

        original.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AllocationSplit>()
            .Which.Reason.Should().Be("ChangeRateFrom");
        second.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AllocationPercentChanged>()
            .Which.Should().Match<AllocationPercentChanged>(e => e.OldPercent == 50m && e.NewPercent == 80m);
    }

    [Fact]
    public void ChangeRateFrom_OutOfRangeRate_Throws()
    {
        var original = Fresh();

        var act = () => original.ChangeRateFrom(new DateOnly(2026, 6, 8), 1001m);

        act.Should().Throw<DomainException>().WithMessage("*range (0, 1000]*");
    }

    // ── "Tolgo la persona a metà" = SplitAt + ConvertToPlaceholder ────────────

    [Fact]
    public void RemovePersonMidSpan_ComposesSplitAndConvert()
    {
        var original = Fresh(percent: 50m);
        var date = new DateOnly(2026, 6, 8);

        var second = original.SplitAt(date);
        second.ConvertToPlaceholder(RoleSkill, Owner);

        // First half: still assigned to the resource.
        original.IsPlaceholder.Should().BeFalse();
        original.ResourceId.Should().Be(Resource);
        original.PeriodEnd.Should().Be(date.AddDays(-1));

        // Second half: now an open role, same span/rate.
        second.IsPlaceholder.Should().BeTrue();
        second.RoleSkillId.Should().Be(RoleSkill);
        second.PeriodStart.Should().Be(date);
        second.PeriodEnd.Should().Be(End);
        second.AllocationPercent.Should().Be(50m);
    }

    // ── Shift: translate the body ─────────────────────────────────────────────

    [Fact]
    public void Shift_TranslatesBothEdges_PreservesDurationAndRate()
    {
        var a = Fresh(percent: 50m);
        var originalDuration = a.PeriodEnd.DayNumber - a.PeriodStart.DayNumber;

        a.Shift(7);

        a.PeriodStart.Should().Be(Start.AddDays(7));
        a.PeriodEnd.Should().Be(End.AddDays(7));
        (a.PeriodEnd.DayNumber - a.PeriodStart.DayNumber).Should().Be(originalDuration);
        a.AllocationPercent.Should().Be(50m);
        a.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AllocationPeriodChanged>()
            .Which.Reason.Should().Be("Shift");
    }

    [Fact]
    public void Shift_NegativeDelta_MovesEarlier()
    {
        var a = Fresh();

        a.Shift(-3);

        a.PeriodStart.Should().Be(Start.AddDays(-3));
        a.PeriodEnd.Should().Be(End.AddDays(-3));
    }

    [Fact]
    public void Shift_ZeroDelta_IsNoOp()
    {
        var a = Fresh();

        a.Shift(0);

        a.PeriodStart.Should().Be(Start);
        a.PeriodEnd.Should().Be(End);
        a.DomainEvents.Should().BeEmpty();
    }

    // ── Resize: move one edge ─────────────────────────────────────────────────

    [Fact]
    public void Resize_MovesEnd_KeepsStart()
    {
        var a = Fresh();
        var newEnd = new DateOnly(2026, 6, 20);

        a.Resize(newStart: null, newEnd: newEnd);

        a.PeriodStart.Should().Be(Start);
        a.PeriodEnd.Should().Be(newEnd);
        a.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AllocationPeriodChanged>()
            .Which.Reason.Should().Be("Resize");
    }

    [Fact]
    public void Resize_MovesStart_KeepsEnd()
    {
        var a = Fresh();
        var newStart = new DateOnly(2026, 6, 5);

        a.Resize(newStart: newStart, newEnd: null);

        a.PeriodStart.Should().Be(newStart);
        a.PeriodEnd.Should().Be(End);
    }

    [Fact]
    public void Resize_NeitherEdge_Throws()
    {
        var a = Fresh();

        var act = () => a.Resize(null, null);

        act.Should().Throw<DomainException>().WithMessage("*at least one*");
    }

    [Fact]
    public void Resize_ProducingStartAfterEnd_Throws()
    {
        var a = Fresh();

        var act = () => a.Resize(newStart: new DateOnly(2026, 6, 20), newEnd: null); // start > end

        act.Should().Throw<DomainException>().WithMessage("*on or before*");
    }

    // ── Cascade properties: gaps preserved, overlap sums ──────────────────────

    [Fact]
    public void UniformShift_PreservesRelativeGaps()
    {
        // Lane of three blocks with gaps between them.
        var b1 = Fresh(start: new DateOnly(2026, 6, 1), end: new DateOnly(2026, 6, 5));
        var b2 = Fresh(start: new DateOnly(2026, 6, 10), end: new DateOnly(2026, 6, 12));
        var b3 = Fresh(start: new DateOnly(2026, 6, 20), end: new DateOnly(2026, 6, 25));

        int Gap(Allocation earlier, Allocation later) =>
            later.PeriodStart.DayNumber - earlier.PeriodEnd.DayNumber;

        var gap12 = Gap(b1, b2);
        var gap23 = Gap(b2, b3);

        const int delta = 9;
        foreach (var b in new[] { b1, b2, b3 }) b.Shift(delta);

        Gap(b1, b2).Should().Be(gap12);
        Gap(b2, b3).Should().Be(gap23);
        b1.PeriodStart.Should().Be(new DateOnly(2026, 6, 10));
    }

    [Fact]
    public void OverlapFromResize_Sums_AndDoesNotThrow()
    {
        // Two adjacent blocks; resize the first to overlap the second.
        var first = Fresh(percent: 50m, start: new DateOnly(2026, 6, 1), end: new DateOnly(2026, 6, 5));
        var second = Fresh(percent: 50m, start: new DateOnly(2026, 6, 6), end: new DateOnly(2026, 6, 10));

        var act = () => first.Resize(newStart: null, newEnd: new DateOnly(2026, 6, 8)); // now overlaps [6,8]
        act.Should().NotThrow();

        // On the overlapping days the rate% sums (ADR-0014) — not blocked.
        foreach (var d in DaysInclusive(new DateOnly(2026, 6, 6), new DateOnly(2026, 6, 8)))
            RateSumOn(d, first, second).Should().Be(100m);
    }
}
