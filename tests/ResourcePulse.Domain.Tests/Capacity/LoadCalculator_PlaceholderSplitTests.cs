using ResourcePulse.Domain.Allocations;

namespace ResourcePulse.Domain.Tests.Capacity;

// ADR-0016 §5: lo split per-resource (esclude placeholder) vs per-node
// (include i placeholder, contributo separato in PlaceholderRatePercent).
public class LoadCalculator_PlaceholderSplitTests
{
    private static readonly Guid R1 = Guid.NewGuid();
    private static readonly Guid Node = Guid.NewGuid();
    private static readonly Guid RoleSkill = Guid.NewGuid();
    private static readonly DateOnly Mon = new(2026, 6, 1);
    private static readonly DateOnly Fri = new(2026, 6, 5);

    private static Dictionary<DateOnly, TimeSpan> CapacityByDate(TimeSpan daily)
    {
        var d = new Dictionary<DateOnly, TimeSpan>();
        for (var dt = Mon; dt <= Fri; dt = dt.AddDays(1)) d[dt] = daily;
        return d;
    }

    private static Dictionary<(Guid, DateOnly), TimeSpan> CapacityByResourceAndDate(
        Dictionary<Guid, TimeSpan> perResourceDaily)
    {
        var d = new Dictionary<(Guid, DateOnly), TimeSpan>();
        for (var dt = Mon; dt <= Fri; dt = dt.AddDays(1))
            foreach (var (rid, hrs) in perResourceDaily)
                d[(rid, dt)] = hrs;
        return d;
    }

    // ── ForResourceAndDate / ForResourceAndRange exclude placeholders ───────

    [Fact]
    public void ForResourceAndDate_PlaceholderOnSameNode_NotCountedTowardResourceLoad()
    {
        // R1 has no real allocation; only a placeholder exists on the node.
        var ph = Allocation.CreatePlaceholder(Node, Mon, Fri, 50m, RoleSkill, ownerResourceId: null);

        var hours = LoadCalculator.ForResourceAndDate(R1, [ph], TimeSpan.FromHours(8), Mon);

        hours.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ForResourceAndRange_PlaceholdersExcluded_OnlyAssignedContribute()
    {
        var assigned = Allocation.Create(R1, Node, Mon, Fri, 50m);
        var placeholder = Allocation.CreatePlaceholder(Node, Mon, Fri, 80m, RoleSkill, null);

        var result = LoadCalculator
            .ForResourceAndRange(R1, [assigned, placeholder], CapacityByDate(TimeSpan.FromHours(8)), Mon, Fri)
            .ToList();

        result.Should().HaveCount(5);
        // 50% of 8h = 4h per day — only the assigned counts.
        result.Should().AllSatisfy(d => d.Hours.Should().Be(TimeSpan.FromHours(4)));
    }

    // ── ForProjectNodeAndRange INCLUDES placeholders ────────────────────────

    [Fact]
    public void ForProjectNodeAndRange_PlaceholderAlone_TotalHoursZero_PlaceholderPercentSet()
    {
        var ph = Allocation.CreatePlaceholder(Node, Mon, Fri, 75m, RoleSkill, null);

        var result = LoadCalculator
            .ForProjectNodeAndRange(Node, [ph],
                CapacityByResourceAndDate(new() { [R1] = TimeSpan.FromHours(8) }),
                Mon, Fri)
            .ToList();

        result.Should().AllSatisfy(d =>
        {
            d.TotalHours.Should().Be(TimeSpan.Zero);   // niente risorsa ⇒ niente ore
            d.ByResource.Should().BeEmpty();
            d.PlaceholderRatePercent.Should().Be(75m);
        });
    }

    [Fact]
    public void ForProjectNodeAndRange_AssignedPlusPlaceholder_SplitReported()
    {
        var assigned = Allocation.Create(R1, Node, Mon, Fri, 50m);
        var ph = Allocation.CreatePlaceholder(Node, Mon, Fri, 30m, RoleSkill, null);

        var result = LoadCalculator
            .ForProjectNodeAndRange(Node, [assigned, ph],
                CapacityByResourceAndDate(new() { [R1] = TimeSpan.FromHours(8) }),
                Mon, Fri)
            .ToList();

        result.Should().AllSatisfy(d =>
        {
            d.TotalHours.Should().Be(TimeSpan.FromHours(4));   // R1 50% of 8h
            d.ByResource.Should().HaveCount(1);
            d.ByResource[R1].Should().Be(TimeSpan.FromHours(4));
            d.PlaceholderRatePercent.Should().Be(30m);
        });
    }

    [Fact]
    public void ForProjectNodeAndRange_MultiplePlaceholders_RatePercentsSum()
    {
        // Two placeholders on same date+node: rate% sums (consistent with ADR-0014).
        var ph1 = Allocation.CreatePlaceholder(Node, Mon, Fri, 40m, RoleSkill, null);
        var ph2 = Allocation.CreatePlaceholder(Node, Mon, Fri, 25m, RoleSkill, null);

        var result = LoadCalculator
            .ForProjectNodeAndRange(Node, [ph1, ph2],
                CapacityByResourceAndDate(new()),
                Mon, Fri)
            .ToList();

        result.Should().AllSatisfy(d =>
        {
            d.TotalHours.Should().Be(TimeSpan.Zero);
            d.PlaceholderRatePercent.Should().Be(65m);
        });
    }

    [Fact]
    public void ForProjectNodeAndRange_NoPlaceholders_PlaceholderPercentIsZero()
    {
        var assigned = Allocation.Create(R1, Node, Mon, Fri, 50m);

        var result = LoadCalculator
            .ForProjectNodeAndRange(Node, [assigned],
                CapacityByResourceAndDate(new() { [R1] = TimeSpan.FromHours(8) }),
                Mon, Fri)
            .ToList();

        result.Should().AllSatisfy(d => d.PlaceholderRatePercent.Should().Be(0m));
    }
}
