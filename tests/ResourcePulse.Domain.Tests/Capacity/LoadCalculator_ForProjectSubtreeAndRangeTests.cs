namespace ResourcePulse.Domain.Tests.Capacity;

// Subtree aggregation (ADR-0022 / gap #5): ForProjectSubtreeAndRange sums every
// block the caller hands it, regardless of which node each sits on — the caller
// has already scoped to the subtree via the Path prefix. Contrast with
// ForProjectNodeAndRange, which keeps only the exact node's blocks.
public class LoadCalculator_ForProjectSubtreeAndRangeTests
{
    private static readonly Guid R1 = Guid.NewGuid();
    private static readonly Guid R2 = Guid.NewGuid();
    private static readonly Guid Root = Guid.NewGuid();   // Project
    private static readonly Guid Phase = Guid.NewGuid();  // child Phase
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
    public void SumsBlocksAcrossNodes_RootAndPhase()
    {
        // R1 @ 50% on the root, R2 @ 100% on a child Phase. The subtree aggregate
        // must include BOTH. Per day with 8h capacity: R1 = 4h, R2 = 8h, total 12h.
        var atRoot = Coverage.Cov(R1, Root, Mon, Fri, 50m);
        var atPhase = Coverage.Cov(R2, Phase, Mon, Fri, 100m);
        var capacity = Capacity(new()
        {
            [R1] = TimeSpan.FromHours(8),
            [R2] = TimeSpan.FromHours(8),
        });

        var result = LoadCalculator
            .ForProjectSubtreeAndRange([atRoot, atPhase], capacity, Mon, Fri)
            .ToList();

        result.Should().HaveCount(5);
        result.Should().AllSatisfy(d =>
        {
            d.TotalHours.Should().Be(TimeSpan.FromHours(12));
            d.ByResource[R1].Should().Be(TimeSpan.FromHours(4));
            d.ByResource[R2].Should().Be(TimeSpan.FromHours(8));
        });
    }

    [Fact]
    public void SameResource_OnRootAndPhase_SumsIntoOneByResourceEntry()
    {
        // The same person staffed on both the root and a phase of the same project
        // sums into a single per-resource entry (ADR-0014 composition is uniform,
        // and here it spans nodes within the subtree).
        var atRoot = Coverage.Cov(R1, Root, Mon, Mon, 50m);
        var atPhase = Coverage.Cov(R1, Phase, Mon, Mon, 30m);
        var capacity = Capacity(new() { [R1] = TimeSpan.FromHours(8) });

        var result = LoadCalculator
            .ForProjectSubtreeAndRange([atRoot, atPhase], capacity, Mon, Mon)
            .ToList();

        // 50% + 30% of 8h = 4 + 2.4 = 6.4h, one entry for R1.
        result[0].ByResource.Should().HaveCount(1);
        result[0].ByResource[R1].Should().Be(TimeSpan.FromHours(6.4));
        result[0].TotalHours.Should().Be(TimeSpan.FromHours(6.4));
    }

    [Fact]
    public void ExactNodeMethod_DropsPhaseBlocks_WhereasSubtreeKeepsThem()
    {
        // Demonstrates the gap #5 fix: exact-node sees only the root block;
        // subtree sees both.
        var atRoot = Coverage.Cov(R1, Root, Mon, Mon, 50m);
        var atPhase = Coverage.Cov(R2, Phase, Mon, Mon, 50m);
        var capacity = Capacity(new()
        {
            [R1] = TimeSpan.FromHours(8),
            [R2] = TimeSpan.FromHours(8),
        });

        var exact = LoadCalculator
            .ForProjectNodeAndRange(Root, [atRoot, atPhase], capacity, Mon, Mon).ToList();
        var subtree = LoadCalculator
            .ForProjectSubtreeAndRange([atRoot, atPhase], capacity, Mon, Mon).ToList();

        exact[0].TotalHours.Should().Be(TimeSpan.FromHours(4));   // root only
        subtree[0].TotalHours.Should().Be(TimeSpan.FromHours(8)); // root + phase
    }

    [Fact]
    public void FromAfterTo_YieldsNothing()
    {
        var result = LoadCalculator
            .ForProjectSubtreeAndRange([], new Dictionary<(Guid, DateOnly), TimeSpan>(), Fri, Mon)
            .ToList();

        result.Should().BeEmpty();
    }
}
