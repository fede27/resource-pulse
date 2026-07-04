namespace ResourcePulse.Domain.Tests.Capacity;

public class LoadCalculator_ForProjectNodeAndRangeTests
{
    private static readonly Guid R1 = Guid.NewGuid();
    private static readonly Guid R2 = Guid.NewGuid();
    private static readonly Guid R3 = Guid.NewGuid();
    private static readonly Guid Node = Guid.NewGuid();
    private static readonly Guid OtherNode = Guid.NewGuid();
    private static readonly DateOnly Mon = new(2026, 6, 1);
    private static readonly DateOnly Fri = new(2026, 6, 5);

    private static Dictionary<(Guid, DateOnly), TimeSpan> Capacity(
        Dictionary<Guid, TimeSpan> perResourceDaily)
    {
        var d = new Dictionary<(Guid, DateOnly), TimeSpan>();
        for (var dt = Mon; dt <= Fri; dt = dt.AddDays(1))
            foreach (var (rid, hrs) in perResourceDaily)
                d[(rid, dt)] = hrs;
        return d;
    }

    [Fact]
    public void NoAllocations_ZerosForAllDates_EmptyByResource()
    {
        var capacity = Capacity(new() { [R1] = TimeSpan.FromHours(8) });

        var result = LoadCalculator.ForProjectNodeAndRange(Node, [], capacity, Mon, Fri).ToList();

        result.Should().HaveCount(5);
        result.Should().AllSatisfy(d =>
        {
            d.TotalHours.Should().Be(TimeSpan.Zero);
            d.ByResource.Should().BeEmpty();
        });
    }

    [Fact]
    public void OtherNode_NotCounted()
    {
        var theirs = Coverage.Cov(R1, OtherNode, Mon, Fri, 100m);
        var capacity = Capacity(new() { [R1] = TimeSpan.FromHours(8) });

        var result = LoadCalculator.ForProjectNodeAndRange(Node, [theirs], capacity, Mon, Fri).ToList();

        result.Should().AllSatisfy(d => d.TotalHours.Should().Be(TimeSpan.Zero));
    }

    [Fact]
    public void ThreeResources_DifferentPercents_AggregatesCorrectly()
    {
        // R1 @ 50%, R2 @ 100%, R3 @ 25% on the same node, all with 8h capacity.
        // Per day: R1 = 4h, R2 = 8h, R3 = 2h; total = 14h.
        var a1 = Coverage.Cov(R1, Node, Mon, Fri, 50m);
        var a2 = Coverage.Cov(R2, Node, Mon, Fri, 100m);
        var a3 = Coverage.Cov(R3, Node, Mon, Fri, 25m);
        var capacity = Capacity(new()
        {
            [R1] = TimeSpan.FromHours(8),
            [R2] = TimeSpan.FromHours(8),
            [R3] = TimeSpan.FromHours(8),
        });

        var result = LoadCalculator.ForProjectNodeAndRange(Node, [a1, a2, a3], capacity, Mon, Fri).ToList();

        result.Should().HaveCount(5);
        result.Should().AllSatisfy(d =>
        {
            d.TotalHours.Should().Be(TimeSpan.FromHours(14));
            d.ByResource.Should().HaveCount(3);
            d.ByResource[R1].Should().Be(TimeSpan.FromHours(4));
            d.ByResource[R2].Should().Be(TimeSpan.FromHours(8));
            d.ByResource[R3].Should().Be(TimeSpan.FromHours(2));
        });
    }

    [Fact]
    public void DifferentCapacities_PerResource_Honored()
    {
        // R1: 8h base, R2: 4h base (part-time). Both at 50% on Node.
        var a1 = Coverage.Cov(R1, Node, Mon, Mon, 50m);
        var a2 = Coverage.Cov(R2, Node, Mon, Mon, 50m);
        var capacity = new Dictionary<(Guid, DateOnly), TimeSpan>
        {
            [(R1, Mon)] = TimeSpan.FromHours(8),
            [(R2, Mon)] = TimeSpan.FromHours(4),
        };

        var result = LoadCalculator.ForProjectNodeAndRange(Node, [a1, a2], capacity, Mon, Mon).ToList();

        result[0].ByResource[R1].Should().Be(TimeSpan.FromHours(4));
        result[0].ByResource[R2].Should().Be(TimeSpan.FromHours(2));
        result[0].TotalHours.Should().Be(TimeSpan.FromHours(6));
    }

    [Fact]
    public void PartialPeriod_OnlyCoveredDatesContribute()
    {
        // R1 allocated to Node only Wed-Thu at 100%.
        var a = Coverage.Cov(R1, Node, Mon.AddDays(2), Mon.AddDays(3), 100m);
        var capacity = Capacity(new() { [R1] = TimeSpan.FromHours(8) });

        var result = LoadCalculator.ForProjectNodeAndRange(Node, [a], capacity, Mon, Fri).ToList();

        result[0].TotalHours.Should().Be(TimeSpan.Zero);
        result[1].TotalHours.Should().Be(TimeSpan.Zero);
        result[2].TotalHours.Should().Be(TimeSpan.FromHours(8));
        result[3].TotalHours.Should().Be(TimeSpan.FromHours(8));
        result[4].TotalHours.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void FromAfterTo_YieldsNothing()
    {
        var capacity = new Dictionary<(Guid, DateOnly), TimeSpan>();

        var result = LoadCalculator.ForProjectNodeAndRange(Node, [], capacity, Fri, Mon).ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void SameResource_TwoOverlappingBlocks_SameNode_SumInTotalAndByResource()
    {
        // ADR-0014: overlapping blocks on the same (resource, project_node)
        // sum. R1 has two blocks on Node: 50% Mon-Fri + 30% Wed-Thu, 8h capacity.
        // Mon/Tue/Fri -> 4h (50% only). Wed/Thu -> 4 + 2.4 = 6.4h.
        var baseBlock = Coverage.Cov(R1, Node, Mon, Fri, 50m);
        var bump = Coverage.Cov(R1, Node, Mon.AddDays(2), Mon.AddDays(3), 30m);
        var capacity = Capacity(new() { [R1] = TimeSpan.FromHours(8) });

        var result = LoadCalculator.ForProjectNodeAndRange(Node, [baseBlock, bump], capacity, Mon, Fri).ToList();

        result[0].TotalHours.Should().Be(TimeSpan.FromHours(4));
        result[1].TotalHours.Should().Be(TimeSpan.FromHours(4));
        result[2].TotalHours.Should().Be(TimeSpan.FromHours(6.4));
        result[3].TotalHours.Should().Be(TimeSpan.FromHours(6.4));
        result[4].TotalHours.Should().Be(TimeSpan.FromHours(4));

        // The two contributions for R1 collapse into a single ByResource entry
        // (sum), not two separate entries. ADR-0014: composition is uniform.
        result[2].ByResource.Should().HaveCount(1);
        result[2].ByResource[R1].Should().Be(TimeSpan.FromHours(6.4));
    }

    [Fact]
    public void ZeroCapacityForResource_ContributesNothing()
    {
        var a = Coverage.Cov(R1, Node, Mon, Mon, 100m);
        var capacity = new Dictionary<(Guid, DateOnly), TimeSpan>
        {
            [(R1, Mon)] = TimeSpan.Zero
        };

        var result = LoadCalculator.ForProjectNodeAndRange(Node, [a], capacity, Mon, Mon).ToList();

        result[0].TotalHours.Should().Be(TimeSpan.Zero);
        result[0].ByResource.Should().BeEmpty(); // zero contribution is omitted
    }
}
